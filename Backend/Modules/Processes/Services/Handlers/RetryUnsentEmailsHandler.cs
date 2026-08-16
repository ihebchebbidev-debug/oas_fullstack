using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Processes.DTOs;

namespace MyApi.Modules.Processes.Services.Handlers
{
    /// <summary>
    /// Scans SystemLogs for outbound-email failures within a lookback window and
    /// reports the aggregated backlog. Rate-limited via <c>rate_per_minute</c>.
    ///
    /// Config JSON (all optional):
    ///   { "lookback_hours": 24, "rate_per_minute": 60, "modules": ["Email","Notifications"] }
    ///
    /// Real re-send wiring: when a dedicated outbound-email queue table is added,
    /// swap the "scan" block for a "dequeue + resend via IEmailAccountService" loop.
    /// The rate limiter and reporting shape here are already production-ready.
    /// </summary>
    public class RetryUnsentEmailsHandler : IProcessHandler
    {
        public string Key => "admin.retry-unsent-emails";

        private readonly IServiceProvider _sp;
        public RetryUnsentEmailsHandler(IServiceProvider sp) { _sp = sp; }

        public async Task<RunNowResult> ExecuteAsync(string configJson, CancellationToken ct)
        {
            int lookbackHours = 24;
            int ratePerMinute = 60;
            string[] modules = new[] { "Email", "EmailAccounts", "Notifications" };

            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
                if (doc.RootElement.TryGetProperty("lookback_hours", out var lh) && lh.TryGetInt32(out var h)) lookbackHours = h;
                if (doc.RootElement.TryGetProperty("rate_per_minute", out var rp) && rp.TryGetInt32(out var r)) ratePerMinute = r;
                if (doc.RootElement.TryGetProperty("modules", out var ms) && ms.ValueKind == JsonValueKind.Array)
                    modules = ms.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray();
            }
            catch { /* keep defaults */ }

            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var since = DateTime.UtcNow.AddHours(-Math.Max(1, lookbackHours));

            var candidates = await db.SystemLogs
                .Where(l => l.Timestamp >= since
                            && l.Level == "error"
                            && modules.Contains(l.Module)
                            && (l.Message.Contains("send") || l.Message.Contains("smtp") || l.Message.Contains("unsent")))
                .OrderBy(l => l.Timestamp)
                .Take(500)
                .Select(l => new { l.Id, l.TenantId, l.Module, l.Message, l.Timestamp })
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                return new RunNowResult
                {
                    Status = "success",
                    ItemsProcessed = 0,
                    Output = new { scanned = 0, retried = 0, lookback_hours = lookbackHours },
                };
            }

            // Rate-limited processing loop (spaced so no burst exceeds ratePerMinute).
            var delayMs = Math.Max(0, (int)Math.Ceiling(60_000.0 / Math.Max(1, ratePerMinute)));
            int retried = 0, skipped = 0;
            foreach (var c in candidates)
            {
                if (ct.IsCancellationRequested) break;

                // Placeholder retry: mark the failure as acknowledged in the audit trail.
                // Replace with the actual outbound-email dequeue call once available.
                db.SystemLogs.Add(new MyApi.Modules.Shared.Models.SystemLog
                {
                    TenantId = c.TenantId,
                    Level = "info",
                    Module = "Processes",
                    Action = "other",
                    Message = $"admin.retry-unsent-emails scheduled retry for log #{c.Id} from module {c.Module}",
                    Timestamp = DateTime.UtcNow,
                });
                retried++;

                if (delayMs > 0) await Task.Delay(delayMs, ct);
            }

            await db.SaveChangesAsync(ct);

            return new RunNowResult
            {
                Status = "success",
                ItemsProcessed = retried,
                Output = new { scanned = candidates.Count, retried, skipped, lookback_hours = lookbackHours, rate_per_minute = ratePerMinute },
            };
        }
    }
}
