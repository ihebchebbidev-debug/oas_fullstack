using Microsoft.EntityFrameworkCore;
using MyApi.Infrastructure;
using MyApi.Modules.OAS.Common.Realtime;
using MyApi.Modules.OAS.Events.Models;
using MyApi.Modules.OAS.Sla.Models;

namespace MyApi.Modules.OAS.Common.HostedServices;

/// <summary>
/// Replaces `eventStore.ts:226,271-306`'s 30s browser-tab timer (spec §6.3)
/// — SLA escalation only ran when someone had the andon tab open; here it
/// runs server-side regardless. Calls `oas_job_check_sla()` (spec §3:
/// "logique métier critique conservée dans les triggers/fonctions
/// Postgres") for every provisioned *oas tenant database, logs each
/// escalation into oas_escalations, and pushes `event.escalated` on
/// GET /stream — never touches the escalation-level MATH itself, that
/// lives entirely in SQL (floor-based, sequential-only, per spec v15).
///
/// Single-instance only (see OasSseBroadcaster remarks) — if this
/// deployment ever scales to multiple instances, this sweep needs a
/// distributed lock or it will run once per instance and double-log
/// (harmless for the escalation_level itself, since the SQL is
/// idempotent/monotonic, but would double-broadcast and double-insert
/// oas_escalations rows).
/// </summary>
public class OasEscalationSweepHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IOasDbContextFactory _dbFactory;
    private readonly IOasSseBroadcaster _broadcaster;
    private readonly ILogger<OasEscalationSweepHostedService> _logger;

    public OasEscalationSweepHostedService(IOasDbContextFactory dbFactory, IOasSseBroadcaster broadcaster, ILogger<OasEscalationSweepHostedService> logger)
    {
        _dbFactory = dbFactory;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var conn in TenantConnectionResolver.GetConfiguredTenantConnections())
            {
                if (!OasTenant.IsOasSlug(conn.Tenant)) continue;
                await SweepOneAsync(conn.Tenant, stoppingToken);
            }
        }
    }

    private async Task SweepOneAsync(string oasSlug, CancellationToken ct)
    {
        try
        {
            await using var db = _dbFactory.CreateDbContext(oasSlug);

            var escalated = await db.Database.SqlQueryRaw<EscalatedRow>(
                "select * from oas_job_check_sla()").ToListAsync(ct);

            foreach (var row in escalated)
            {
                db.Set<OasEscalation>().Add(new OasEscalation
                {
                    TenantId = row.tenant_id, EventId = row.event_id,
                    Level = Enum.Parse<OasEscalationLevel>(row.new_level, true),
                    Reason = "sla_sweep",
                });

                _broadcaster.Publish(oasSlug, row.tenant_id, "event.escalated", new { eventId = row.event_id, level = row.new_level });
            }

            if (escalated.Count > 0)
            {
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("🏭 OAS-SLA-SWEEP: {Count} event(s) escalated on '{Slug}'", escalated.Count, oasSlug);
            }
        }
        catch (OasTenantNotProvisionedException)
        {
            // Configured env var but no schema applied yet — skip silently, health endpoint already reports this.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🏭 OAS-SLA-SWEEP: failed for tenant '{Slug}'", oasSlug);
        }
    }

    // Matches oas_job_check_sla()'s TABLE(event_id uuid, tenant_id int, new_level oas_escalation_level) return shape.
    private sealed class EscalatedRow
    {
        public Guid event_id { get; set; }
        public int tenant_id { get; set; }
        public string new_level { get; set; } = string.Empty;
    }
}
