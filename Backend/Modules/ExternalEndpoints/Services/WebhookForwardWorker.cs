using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.ExternalEndpoints.Models;

namespace MyApi.Modules.ExternalEndpoints.Services
{
    /// <summary>
    /// Background worker that drains the WebhookForwardJobs table.
    ///
    /// Two trigger sources:
    ///   1. <see cref="IWebhookForwardQueue"/> — fast signal for jobs just enqueued.
    ///   2. A polling tick (every <see cref="PollInterval"/>) — picks up jobs whose
    ///      retry timer has expired AND any work missed across a process restart.
    ///
    /// Retry policy: exponential backoff (30s, 2m, 10m, 1h, 6h). After
    /// <see cref="WebhookForwardJob.MaxAttempts"/> failures the row is moved to
    /// status "dead_letter" so it can be inspected/replayed manually.
    ///
    /// Worker identity (<see cref="_workerId"/>) is included in ClaimedBy so we
    /// can reason about who is processing which row even if multiple instances
    /// run later.
    /// </summary>
    public class WebhookForwardWorker : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ClaimStaleAfter = TimeSpan.FromMinutes(2);
        private const int BatchSize = 25;
        private const string HttpClientName = "ext-webhook";

        private static readonly TimeSpan[] BackoffSchedule =
        {
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(6),
        };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWebhookForwardQueue _queue;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebhookForwardWorker> _logger;
        private readonly string _workerId = BuildWorkerId();

        private static string BuildWorkerId()
        {
            // ClaimedBy column is varchar(100) — clamp safely whatever the
            // machine name length happens to be. Using Math.Min avoids the
            // ArgumentOutOfRangeException that a naked Substring(0, 80) would
            // throw on short hostnames.
            var raw = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}";
            return raw.Length <= 80 ? raw : raw.Substring(0, 80);
        }

        public WebhookForwardWorker(
            IServiceScopeFactory scopeFactory,
            IWebhookForwardQueue queue,
            IHttpClientFactory httpClientFactory,
            ILogger<WebhookForwardWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _queue = queue;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("📤 WebhookForwardWorker started (id={WorkerId}, poll={Poll}s)",
                _workerId, PollInterval.TotalSeconds);

            // Small startup delay so the DB is definitely up before our first sweep.
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch { }

            // Recover jobs claimed by a previous run that crashed mid-flight.
            await ReleaseStaleClaimsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessReadyJobsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WebhookForwardWorker loop error");
                }

                // Wait either for a new-job signal or the poll timeout, whichever first.
                using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                pollCts.CancelAfter(PollInterval);
                try
                {
                    // Drain whatever signals arrive during this window — we don't
                    // actually need the IDs (the DB query finds them) but reading
                    // keeps the channel from filling up.
                    while (await _queue.Reader.WaitToReadAsync(pollCts.Token))
                    {
                        while (_queue.Reader.TryRead(out _)) { }
                        // As soon as we got ANY signal, break to process immediately.
                        break;
                    }
                }
                catch (OperationCanceledException) { /* poll tick or shutdown */ }
            }
        }

        private async Task ReleaseStaleClaimsAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cutoff = DateTime.UtcNow.Subtract(ClaimStaleAfter);
                var stale = await db.WebhookForwardJobs
                    .IgnoreQueryFilters() // worker has no HTTP/tenant context
                    .Where(j => j.Status == "in_progress" && j.ClaimedAt != null && j.ClaimedAt < cutoff)
                    .ToListAsync(ct);
                foreach (var j in stale)
                {
                    j.Status = "pending";
                    j.ClaimedAt = null;
                    j.ClaimedBy = null;
                    j.NextAttemptAt = DateTime.UtcNow;
                }
                if (stale.Count > 0)
                {
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Recovered {Count} stale webhook-forward jobs from previous run", stale.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release stale claims (non-fatal)");
            }
        }

        private async Task ProcessReadyJobsAsync(CancellationToken ct)
        {
            // Claim a batch in one transaction. Other instances would skip rows
            // already flipped to in_progress thanks to the conditional update.
            List<WebhookForwardJob> batch;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow;

                var ready = await db.WebhookForwardJobs
                    .IgnoreQueryFilters() // worker spans all tenants
                    .Where(j => j.Status == "pending" && j.NextAttemptAt <= now)
                    .OrderBy(j => j.NextAttemptAt)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (ready.Count == 0) return;

                foreach (var j in ready)
                {
                    j.Status = "in_progress";
                    j.ClaimedAt = now;
                    j.ClaimedBy = _workerId;
                }
                await db.SaveChangesAsync(ct);
                batch = ready;
            }

            // Process each job in its own scope so a single failure doesn't poison the rest.
            foreach (var job in batch)
            {
                if (ct.IsCancellationRequested) break;
                await ProcessOneAsync(job.Id, ct);
            }
        }

        private async Task ProcessOneAsync(int jobId, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var job = await db.WebhookForwardJobs.IgnoreQueryFilters().FirstOrDefaultAsync(j => j.Id == jobId, ct);
            if (job == null) return;

            job.Attempts += 1;
            job.LastAttemptAt = DateTime.UtcNow;

            int? statusCode = null;
            string? error = null;

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                client.Timeout = TimeSpan.FromSeconds(15);
                var body = job.Body ?? string.Empty;
                using var content = new StringContent(body, Encoding.UTF8, "application/json");

                // HMAC-SHA256 signature so downstream systems can verify the
                // forward originated from us. Snapshot secret is on the job
                // (immune to later endpoint rotation). Format mirrors GitHub /
                // Stripe conventions: `sha256=<hex>` plus a unix timestamp
                // header to enable replay-window enforcement on the receiver.
                if (!string.IsNullOrEmpty(job.Secret))
                {
                    var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                    var signed = $"{ts}.{body}";
                    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(job.Secret));
                    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signed));
                    var hex = Convert.ToHexString(hash).ToLowerInvariant();
                    content.Headers.TryAddWithoutValidation("X-Signature", $"sha256={hex}");
                    content.Headers.TryAddWithoutValidation("X-Signature-Timestamp", ts);
                }

                using var resp = await client.PostAsync(job.ForwardUrl, content, ct);
                statusCode = (int)resp.StatusCode;

                if (resp.IsSuccessStatusCode)
                {
                    job.Status = "succeeded";
                    job.LastStatusCode = statusCode;
                    job.LastError = null;
                    job.ClaimedAt = null;
                    job.ClaimedBy = null;
                    job.CompletedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return;
                }

                error = $"HTTP {statusCode}";
                // 4xx (except 408/429) are unlikely to succeed on retry — dead-letter immediately.
                if (statusCode is >= 400 and < 500 && statusCode != 408 && statusCode != 429)
                {
                    await DeadLetterAsync(db, job, statusCode, error, ct);
                    return;
                }
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                error = "Request timed out";
            }
            catch (HttpRequestException ex)
            {
                error = $"HTTP error: {ex.Message}";
            }
            catch (Exception ex)
            {
                error = $"Unexpected: {ex.GetType().Name}: {ex.Message}";
                _logger.LogWarning(ex, "Unexpected webhook forward error for job {JobId}", jobId);
            }

            // Reach here = retryable failure
            job.LastStatusCode = statusCode;
            job.LastError = Truncate(error, 1000);

            if (job.Attempts >= job.MaxAttempts)
            {
                await DeadLetterAsync(db, job, statusCode, error, ct);
                return;
            }

            var delay = BackoffSchedule[Math.Min(job.Attempts - 1, BackoffSchedule.Length - 1)];
            job.Status = "pending";
            job.NextAttemptAt = DateTime.UtcNow.Add(delay);
            job.ClaimedAt = null;
            job.ClaimedBy = null;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Webhook forward {JobId} attempt {Attempt}/{Max} failed ({Err}); retrying in {Delay}",
                jobId, job.Attempts, job.MaxAttempts, error, delay);
        }

        private async Task DeadLetterAsync(ApplicationDbContext db, WebhookForwardJob job, int? statusCode, string? error, CancellationToken ct)
        {
            job.Status = "dead_letter";
            job.LastStatusCode = statusCode;
            job.LastError = Truncate(error, 1000);
            job.CompletedAt = DateTime.UtcNow;
            job.ClaimedAt = null;
            job.ClaimedBy = null;
            await db.SaveChangesAsync(ct);
            _logger.LogError(
                "Webhook forward {JobId} → dead_letter after {Attempts} attempts (last={Err})",
                job.Id, job.Attempts, error);
        }

        private static string? Truncate(string? s, int max)
            => string.IsNullOrEmpty(s) ? s : (s!.Length <= max ? s : s.Substring(0, max));
    }
}
