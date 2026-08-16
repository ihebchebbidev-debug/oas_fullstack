using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.EmailAccounts.DTOs;
using MyApi.Modules.EmailAccounts.Models;
using MyApi.Modules.EmailAccounts.Services;
using MyApi.Modules.Processes.DTOs;

namespace MyApi.Modules.Processes.Services.Handlers
{
    /// <summary>
    /// Re-sends any <c>OutboundEmailLog</c> row with <c>Status = "failed"</c>,
    /// <c>Attempts &lt; MaxAttempts</c>, and <c>NextRetryAt &lt;= now</c> by replaying
    /// its stored payload through <see cref="EmailAccountService.SendEmailAsync"/>.
    ///
    /// The send method itself writes the outcome (status, attempts, next backoff,
    /// error) — this handler only picks the candidates and drives the replay, so
    /// success/failure logic stays in one place.
    /// </summary>
    public class RetryFailedEmailsHandler : IProcessHandler
    {
        public string Key => "admin.retry-failed-emails";

        private readonly IServiceProvider _sp;
        private readonly ILogger<RetryFailedEmailsHandler> _logger;
        public RetryFailedEmailsHandler(IServiceProvider sp, ILogger<RetryFailedEmailsHandler> logger)
        { _sp = sp; _logger = logger; }

        public async Task<RunNowResult> ExecuteAsync(string configJson, CancellationToken ct)
        {
            int batchSize = ProcessConfigSchemas.GetInt(Key, configJson, "batch_size");

            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var svc = scope.ServiceProvider.GetRequiredService<IEmailAccountService>();

            // Background scopes default to TenantId = 0, which would hide every
            // tenant's ConnectedEmailAccount behind the global query filter and make
            // each retry fail with "Account not found". Start in view-all mode and
            // switch to the log's own tenant before each send so writes stay scoped.
            db.SetTenantId(-1);

            var now = DateTime.UtcNow;

            // Orphaned failed sends whose account or user was deleted after the
            // original attempt can never be retried — transition them to gave_up
            // once so they stop appearing on the "failed emails" dashboard forever.
            var orphaned = await db.OutboundEmailLogs
                .Where(l => l.Status == "failed"
                            && l.Attempts < l.MaxAttempts
                            && (l.AccountId == null || l.UserId == null))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(l => l.Status, "gave_up")
                    .SetProperty(l => l.LastError, "Retry abandoned — the sending account or user no longer exists.")
                    .SetProperty(l => l.LastAttemptAt, now), ct);

            var candidates = await db.OutboundEmailLogs
                .Where(l => l.Status == "failed"
                            && l.Attempts < l.MaxAttempts
                            && (l.NextRetryAt == null || l.NextRetryAt <= now)
                            && l.AccountId != null
                            && l.UserId != null)
                .OrderBy(l => l.NextRetryAt ?? l.CreatedAt)
                .Take(batchSize)
                .ToListAsync(ct);

            int retried = 0, succeeded = 0, stillFailed = 0, gaveUp = orphaned;

            foreach (var log in candidates)
            {
                if (ct.IsCancellationRequested) break;
                SendEmailDto? dto;
                try
                {
                    dto = JsonSerializer.Deserialize<SendEmailDto>(log.PayloadJson);
                }
                catch (Exception ex)
                {
                    log.Status = "gave_up";
                    log.LastError = "Retry aborted — payload could not be deserialized: " + ex.Message;
                    log.LastAttemptAt = DateTime.UtcNow;
                    gaveUp++;
                    continue;
                }
                if (dto == null)
                {
                    log.Status = "gave_up";
                    log.LastError = "Retry aborted — payload deserialized to null.";
                    log.LastAttemptAt = DateTime.UtcNow;
                    gaveUp++;
                    continue;
                }

                retried++;
                // Scope the context to the owning tenant: the account lookup passes the
                // filter and any token refresh the provider performs saves cleanly.
                db.SetTenantId(log.TenantId > 0 ? log.TenantId : -1);
                try
                {
                    // SendEmailAsync updates the same row (attempts, status, error, next retry).
                    var result = await svc.SendEmailAsync(log.AccountId!.Value, log.UserId!.Value, dto, existingLogId: log.Id);
                    if (result.Success) succeeded++;
                    else if (log.Attempts >= log.MaxAttempts) gaveUp++;
                    else stillFailed++;
                }
                catch (Exception ex)
                {
                    // One bad message must never abort the whole batch.
                    _logger.LogWarning(ex, "retry-failed-emails: send threw for log {Id}", log.Id);
                    log.LastError = ex.Message;
                    log.LastAttemptAt = DateTime.UtcNow;
                    stillFailed++;
                }
            }

            // OutboundEmailLog is not tenant-scoped, so this save is safe in view-all mode.
            db.SetTenantId(-1);
            // Use None, not ct: on cancellation the loop breaks with 'gave_up' / error
            // bookkeeping still pending. Saving with a cancelled token would throw and
            // lose it, so those messages would be retried forever.
            await db.SaveChangesAsync(CancellationToken.None);


            _logger.LogInformation("📧 retry-failed-emails: retried={Retried} succeeded={Ok} stillFailed={Fail} gaveUp={Gave}",
                retried, succeeded, stillFailed, gaveUp);

            return new RunNowResult
            {
                Status = "success",
                ItemsProcessed = retried,
                Output = new { retried, succeeded, still_failed = stillFailed, gave_up = gaveUp, candidates = candidates.Count },
            };
        }
    }
}
