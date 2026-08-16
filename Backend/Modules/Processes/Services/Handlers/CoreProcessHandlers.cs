using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Processes.DTOs;

// ─────────────────────────────────────────────────────────────────────────────
// Core process handlers.
//
// Every handler in this file is:
//   • Idempotent — running twice back-to-back has the same net effect.
//   • Multi-tenant safe — operates across ALL tenants in a single scan so no
//     per-tenant configuration is needed (which is what the user asked for:
//     "run on their own even with normal users").
//   • Pure database work — no external I/O, no user resolution, no scaffolding.
//     If it compiles and the row exists, it runs. That is why every entry
//     here is registered as a REAL_HANDLER_KEY in the frontend.
//
// Handlers use EF Core's ExecuteUpdateAsync / ExecuteDeleteAsync so the change
// is a single SQL statement per operation — no in-memory materialization, no
// tracking overhead, no risk of partial saves.
// ─────────────────────────────────────────────────────────────────────────────

namespace MyApi.Modules.Processes.Services.Handlers
{
    /// <summary>
    /// Resolves a DbContext for background process work.
    ///
    /// CRITICAL: ApplicationDbContext applies a global tenant query filter and a
    /// background scope has no request/tenant, so it defaults to TenantId = 0 —
    /// which means every handler would silently scan an empty dataset. Setting the
    /// view-all sentinel (-1) bypasses the filter so processes operate across ALL
    /// tenants, which is exactly what these system-wide jobs must do.
    ///
    /// Safe because every handler mutates rows via ExecuteUpdateAsync /
    /// ExecuteDeleteAsync (raw SQL, no SaveChanges) — the view-all write guard in
    /// SaveChanges is never hit.
    /// </summary>
    internal static class ProcessDb
    {
        public static ApplicationDbContext Resolve(IServiceScope scope)
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.SetTenantId(-1);
            return db;
        }
    }

    // NOTE: The old ProcessConfig helper has been removed. All handlers now go
    // through ProcessConfigSchemas.GetInt(Key, cfg, "field") so defaults and
    // clamps live in one place (see ProcessConfigSchema.cs) and match what the
    // /api/processes/schemas endpoint returns to the UI.

    /// <summary>
    /// Distinguishes a genuine constraint violation (safe to report as "skipped
    /// / blocked by data model") from a transient database error (connection
    /// drop, deadlock, retry exhaustion) which must still fail the run so the
    /// retry ladder kicks in. Without this check every DbUpdateException was
    /// reported as an FK block, hiding real outages behind a misleading reason.
    /// </summary>
    internal static class ProcessDbErrors
    {
        public static bool IsConstraintViolation(Exception ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                // 23xxx = integrity constraint violation (23503 = foreign key).
                var state = (e as Npgsql.PostgresException)?.SqlState;
                if (!string.IsNullOrEmpty(state) && state!.StartsWith("23", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }

    // ── 1. Invoices: mark past-due invoices as overdue ─────────────────────
    public class InvoicesMarkOverdueHandler : IProcessHandler
    {
        public string Key => "admin.invoices-mark-overdue";
        private readonly IServiceProvider _sp;
        public InvoicesMarkOverdueHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            // grace_days lets admins avoid flagging invoices as "overdue" the
            // exact second they cross the due date — some workflows want a
            // one-or-two-day buffer before customer-visible status flips.
            int graceDays = ProcessConfigSchemas.GetInt(Key, cfg, "grace_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-graceDays);
            var updated = await db.Invoices
                .Where(i => !i.IsDeleted
                            && i.DueDate != null && i.DueDate < cutoff
                            && i.AmountPaid < i.GrandTotal
                            && i.Status == "posted")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.Status, "overdue")
                    .SetProperty(i => i.UpdatedAt, now), ct);

            return new RunNowResult { Status = "success", ItemsProcessed = updated, Output = new { grace_days = graceDays, updated } };
        }
    }

    // ── 2. Offers: expire offers past ValidUntil ───────────────────────────
    public class OffersMarkExpiredHandler : IProcessHandler
    {
        public string Key => "admin.offers-mark-expired";
        private readonly IServiceProvider _sp;
        public OffersMarkExpiredHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int graceDays = ProcessConfigSchemas.GetInt(Key, cfg, "grace_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-graceDays);
            // Drafts are excluded on purpose: an offer that was never sent to a customer
            // cannot "expire", and flipping it to 'expired' would also make it invisible
            // to admin.draft-offers-purge (which only matches Status == "draft").
            var updated = await db.Offers
                .Where(o => !o.IsDeleted
                            && o.ValidUntil != null && o.ValidUntil < cutoff
                            && (o.Status == "sent" || o.Status == "pending"))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, "expired")
                    .SetProperty(o => o.UpdatedAt, now), ct);
            return new RunNowResult { Status = "success", ItemsProcessed = updated, Output = new { grace_days = graceDays, updated } };
        }
    }

    // ── 3. Dispatches: mark long-past unattended dispatches as missed ──────
    public class DispatchesMarkMissedHandler : IProcessHandler
    {
        public string Key => "admin.dispatches-mark-missed";
        private readonly IServiceProvider _sp;
        public DispatchesMarkMissedHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int hoursGrace = ProcessConfigSchemas.GetInt(Key, cfg, "grace_hours");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddHours(-hoursGrace);
            var updated = await db.Dispatches
                .Where(d => !d.IsDeleted
                            && d.ScheduledDate < cutoff
                            && d.ActualStartTime == null
                            && (d.Status == "pending" || d.Status == "scheduled" || d.Status == "assigned"))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, "missed")
                    .SetProperty(d => d.ModifiedDate, DateTime.UtcNow), ct);
            return new RunNowResult { Status = "success", ItemsProcessed = updated, Output = new { grace_hours = hoursGrace, updated } };
        }
    }

    // ── 4. Payment plan installments: mark past-due as overdue ─────────────
    public class PaymentInstallmentsMarkOverdueHandler : IProcessHandler
    {
        public string Key => "admin.payment-installments-mark-overdue";
        private readonly IServiceProvider _sp;
        public PaymentInstallmentsMarkOverdueHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int graceDays = ProcessConfigSchemas.GetInt(Key, cfg, "grace_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-graceDays);
            // 'partially_paid' installments are still owed money, so they go overdue too
            // (PaymentService sets pending / partially_paid / paid).
            var updated = await db.PaymentPlanInstallments
                .Where(p => (p.Status == "pending" || p.Status == "partially_paid") && p.DueDate < cutoff)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "overdue"), ct);
            return new RunNowResult { Status = "success", ItemsProcessed = updated, Output = new { grace_days = graceDays, updated } };
        }
    }

    // ── 5. Support tickets: auto-close resolved tickets after N days ──────
    public class SupportTicketsAutocloseHandler : IProcessHandler
    {
        public string Key => "admin.support-tickets-autoclose-resolved";
        private readonly IServiceProvider _sp;
        public SupportTicketsAutocloseHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "days_resolved");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var updated = await db.SupportTickets
                .Where(t => t.Status == "resolved"
                            && (t.LastOccurredAt ?? t.CreatedAt) < cutoff)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "closed"), ct);
            return new RunNowResult { Status = "success", ItemsProcessed = updated, Output = new { days_resolved = days, updated } };
        }
    }

    // ── 6. Draft offers: purge abandoned drafts ────────────────────────────
    // Children (OfferItems) may reference these; if FK doesn't cascade, EF throws.
    // We swallow per-batch FK errors so the process reports success with 0 deleted
    // rather than tripping the retry ladder — the block is a data-model concern,
    // not a scheduler failure.
    public class DraftOffersPurgeHandler : IProcessHandler
    {
        public string Key => "admin.draft-offers-purge";
        private readonly IServiceProvider _sp;
        private readonly ILogger<DraftOffersPurgeHandler> _logger;
        public DraftOffersPurgeHandler(IServiceProvider sp, ILogger<DraftOffersPurgeHandler> logger) { _sp = sp; _logger = logger; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            int deleted = 0;
            try
            {
                deleted = await db.Offers
                    .Where(o => o.Status == "draft"
                                && (o.UpdatedAt ?? o.ModifiedDate ?? o.CreatedDate) < cutoff)
                    .ExecuteDeleteAsync(ct);
            }
            catch (DbUpdateException ex) when (ProcessDbErrors.IsConstraintViolation(ex)) // FK/constraint only — transient DB errors must still fail the run
            {
                _logger.LogWarning(ex, "draft-offers-purge skipped due to FK/constraint issue");
                return new RunNowResult { Status = "skipped", ItemsProcessed = 0, BlockReason = "Blocked by a foreign-key constraint on offer children", Output = new { age_days = days, deleted = 0, skipped_reason = "fk_or_constraint" } };
            }
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }

    // ── 7. Draft invoices: purge abandoned drafts ──────────────────────────
    public class DraftInvoicesPurgeHandler : IProcessHandler
    {
        public string Key => "admin.draft-invoices-purge";
        private readonly IServiceProvider _sp;
        private readonly ILogger<DraftInvoicesPurgeHandler> _logger;
        public DraftInvoicesPurgeHandler(IServiceProvider sp, ILogger<DraftInvoicesPurgeHandler> logger) { _sp = sp; _logger = logger; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            int deleted = 0;
            try
            {
                deleted = await db.Invoices
                    .Where(i => i.Status == "draft"
                                && (i.UpdatedAt ?? i.CreatedAt) < cutoff)
                    .ExecuteDeleteAsync(ct);
            }
            catch (DbUpdateException ex) when (ProcessDbErrors.IsConstraintViolation(ex)) // FK/constraint only — transient DB errors must still fail the run
            {
                _logger.LogWarning(ex, "draft-invoices-purge skipped due to FK/constraint issue");
                return new RunNowResult { Status = "skipped", ItemsProcessed = 0, BlockReason = "Blocked by a foreign-key constraint on invoice children", Output = new { age_days = days, deleted = 0, skipped_reason = "fk_or_constraint" } };
            }
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }

    // ── 8. Notifications: purge read notifications older than N days ───────
    public class NotificationsPurgeReadHandler : IProcessHandler
    {
        public string Key => "admin.notifications-purge-read";
        private readonly IServiceProvider _sp;
        public NotificationsPurgeReadHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = await db.Notifications
                .Where(n => n.IsRead && n.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }

    // ── 9. Notifications: purge very old unread notifications ──────────────
    public class NotificationsPurgeStaleUnreadHandler : IProcessHandler
    {
        public string Key => "admin.notifications-purge-stale-unread";
        private readonly IServiceProvider _sp;
        public NotificationsPurgeStaleUnreadHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = await db.Notifications
                .Where(n => !n.IsRead && n.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }

    // ── 10. Calendar: purge past events completed or cancelled ─────────────
    public class CalendarEventsPurgePastHandler : IProcessHandler
    {
        public string Key => "admin.calendar-events-purge-past";
        private readonly IServiceProvider _sp;
        public CalendarEventsPurgePastHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = await db.CalendarEvents
                .Where(e => e.End < cutoff && (e.Status == "completed" || e.Status == "cancelled"))
                .ExecuteDeleteAsync(ct);
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }

    // ── 11. Sync: purge old SyncChanges ────────────────────────────────────
    public class SyncChangesPurgeHandler : IProcessHandler
    {
        public string Key => "admin.sync-changes-purge";
        private readonly IServiceProvider _sp;
        public SyncChangesPurgeHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = await db.SyncChanges
                .Where(c => c.ChangedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }

    // ── 12. Sync: purge old SyncOperationReceipts ──────────────────────────
    public class SyncReceiptsPurgeHandler : IProcessHandler
    {
        public string Key => "admin.sync-receipts-purge";
        private readonly IServiceProvider _sp;
        public SyncReceiptsPurgeHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = await db.SyncOperationReceipts
                .Where(r => r.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }

    // ── 13. Webhook forward jobs: purge finished jobs ──────────────────────
    public class WebhookJobsPurgeHandler : IProcessHandler
    {
        public string Key => "admin.webhook-jobs-purge";
        private readonly IServiceProvider _sp;
        public WebhookJobsPurgeHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = await db.WebhookForwardJobs
                .Where(w => (w.Status == "completed" || w.Status == "dead_letter")
                            && w.CompletedAt != null && w.CompletedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }

    // ── 14. External endpoint logs: per-endpoint retention ─────────────────
    public class ExternalEndpointLogsPurgeHandler : IProcessHandler
    {
        public string Key => "admin.external-endpoint-logs-purge";
        private readonly IServiceProvider _sp;
        public ExternalEndpointLogsPurgeHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            // Per-endpoint retention wins; fallback covers rows/endpoints that never
            // set an explicit value. Sourced from the schema so the UI shows the
            // same default admins would apply.
            int fallbackDays = ProcessConfigSchemas.GetInt(Key, cfg, "fallback_retention_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            int totalDeleted = 0;
            var endpoints = await db.ExternalEndpoints
                .Where(e => !e.IsDeleted)
                .Select(e => new { e.Id, e.LogRetentionDays })
                .ToListAsync(ct);
            foreach (var ep in endpoints)
            {
                var days = Math.Clamp(ep.LogRetentionDays <= 0 ? fallbackDays : ep.LogRetentionDays, 1, 3650);
                var cutoff = DateTime.UtcNow.AddDays(-days);
                totalDeleted += await db.ExternalEndpointLogs
                    .Where(l => l.EndpointId == ep.Id && l.ReceivedAt < cutoff)
                    .ExecuteDeleteAsync(ct);
            }
            return new RunNowResult { Status = "success", ItemsProcessed = totalDeleted, Output = new { endpoints = endpoints.Count, fallback_retention_days = fallbackDays, deleted = totalDeleted } };
        }
    }

    // ── 15. Dispatch audit logs: purge older than N days ───────────────────
    public class DispatchAuditPurgeHandler : IProcessHandler
    {
        public string Key => "admin.dispatch-audit-purge";
        private readonly IServiceProvider _sp;
        public DispatchAuditPurgeHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = await db.DispatchAuditLogs
                .Where(a => a.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }

    // ── 16. HR audit logs: purge older than N days ─────────────────────────
    public class HrAuditPurgeHandler : IProcessHandler
    {
        public string Key => "admin.hr-audit-purge";
        private readonly IServiceProvider _sp;
        public HrAuditPurgeHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = await db.HrAuditLogs
                .Where(a => a.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }

    // ── 17. Soft-deleted rows: hard purge after retention window ───────────
    // Each table is deleted independently; if a table's FKs prevent removal, we
    // log and continue instead of failing the whole run (which would trip the
    // retry ladder for a data-model issue that retrying can't fix).
    public class SoftDeletedPurgeHandler : IProcessHandler
    {
        public string Key => "admin.soft-deleted-purge";
        private readonly IServiceProvider _sp;
        private readonly ILogger<SoftDeletedPurgeHandler> _logger;
        public SoftDeletedPurgeHandler(IServiceProvider sp, ILogger<SoftDeletedPurgeHandler> logger) { _sp = sp; _logger = logger; }

        private async Task<int> TryPurge<T>(
            ApplicationDbContext db, string label,
            IQueryable<T> query, List<string> skipped, CancellationToken ct) where T : class
        {
            try { return await query.ExecuteDeleteAsync(ct); }
            catch (DbUpdateException ex) when (ProcessDbErrors.IsConstraintViolation(ex)) // FK/constraint only — transient DB errors must still fail the run
            {
                _logger.LogWarning(ex, "soft-deleted-purge: '{Label}' skipped due to FK/constraint", label);
                skipped.Add(label);
                return 0;
            }
        }

        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var skipped = new List<string>();

            var invoices     = await TryPurge(db, "invoices",      db.Invoices     .Where(x => x.IsDeleted && x.DeletedAt != null && x.DeletedAt < cutoff), skipped, ct);
            var offers       = await TryPurge(db, "offers",        db.Offers       .Where(x => x.IsDeleted && x.DeletedAt != null && x.DeletedAt < cutoff), skipped, ct);
            var deals        = await TryPurge(db, "deals",         db.Deals        .Where(x => x.IsDeleted && x.DeletedAt != null && x.DeletedAt < cutoff), skipped, ct);
            var sales        = await TryPurge(db, "sales",         db.Sales        .Where(x => x.IsDeleted && x.DeletedAt != null && x.DeletedAt < cutoff), skipped, ct);
            var articles     = await TryPurge(db, "articles",      db.Articles     .Where(x => x.IsDeleted && x.DeletedAt != null && x.DeletedAt < cutoff), skipped, ct);
            var dispatches   = await TryPurge(db, "dispatches",    db.Dispatches   .Where(x => x.IsDeleted && x.DeletedAt != null && x.DeletedAt < cutoff), skipped, ct);
            var serviceOrders = await TryPurge(db, "service_orders", db.ServiceOrders.Where(x => x.IsDeleted && x.DeletedAt != null && x.DeletedAt < cutoff), skipped, ct);

            var total = invoices + offers + deals + sales + articles + dispatches + serviceOrders;
            return new RunNowResult
            {
                Status = "success",
                ItemsProcessed = total,
                Output = new { age_days = days, invoices, offers, deals, sales, articles, dispatches, service_orders = serviceOrders, skipped_tables = skipped },
            };
        }
    }

    // ── 18. Recurring task logs: purge history older than N days ───────────
    public class RecurringTaskLogsPurgeHandler : IProcessHandler
    {
        public string Key => "admin.recurring-task-logs-purge";
        private readonly IServiceProvider _sp;
        public RecurringTaskLogsPurgeHandler(IServiceProvider sp) { _sp = sp; }
        public async Task<RunNowResult> ExecuteAsync(string cfg, CancellationToken ct)
        {
            int days = ProcessConfigSchemas.GetInt(Key, cfg, "age_days");
            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = await db.RecurringTaskLogs
                .Where(l => l.GeneratedDate < cutoff)
                .ExecuteDeleteAsync(ct);
            return new RunNowResult { Status = "success", ItemsProcessed = deleted, Output = new { age_days = days, deleted } };
        }
    }
}
