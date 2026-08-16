using Microsoft.EntityFrameworkCore;
using MyApi.Infrastructure;
using MyApi.Modules.OAS.Common.Realtime;
using MyApi.Modules.OAS.PostSessions.Models;
using MyApi.Modules.OAS.Shifts.Models;

namespace MyApi.Modules.OAS.Common.HostedServices;

/// <summary>
/// Replaces `session.ts:319-331`'s 60s browser timer (`useDeclarationReminder`,
/// `autoCloseEndedShift`) — spec §6.3. v15 fix, the whole reason this
/// exists server-side rather than being a straight port: the client
/// measured its grace period from `session.startedAt` instead of the
/// shift's actual end time, so a session opened at shift start would
/// auto-close within a minute of the shift ending instead of ~15 minutes
/// later. This service measures from the shift's real end timestamp.
/// It only ever CLOSES a session (ends it cleanly, same as
/// POST /post-sessions/{id}/close) — it never discards a session's
/// declarations, which are already persisted server-side the moment
/// they're created (spec Lot 4), unlike the old client's in-memory queue
/// that `closeSession()` used to wipe.
/// </summary>
public class OasSessionWatchdogHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(15);

    private readonly IOasDbContextFactory _dbFactory;
    private readonly IOasSseBroadcaster _broadcaster;
    private readonly ILogger<OasSessionWatchdogHostedService> _logger;

    public OasSessionWatchdogHostedService(IOasDbContextFactory dbFactory, IOasSseBroadcaster broadcaster, ILogger<OasSessionWatchdogHostedService> logger)
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

            var activeSessions = await db.Set<OasPostSession>()
                .Where(s => s.EndedAt == null && s.ShiftTemplateId != null)
                .ToListAsync(ct);
            if (activeSessions.Count == 0) return;

            var shiftIds = activeSessions.Select(s => s.ShiftTemplateId!.Value).Distinct().ToList();
            var shifts = await db.Set<OasShiftTemplate>().Where(t => shiftIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);

            var now = DateTimeOffset.UtcNow;
            var closedCount = 0;

            foreach (var session in activeSessions)
            {
                if (!shifts.TryGetValue(session.ShiftTemplateId!.Value, out var shift)) continue;

                var shiftEndUtc = ShiftEndUtc(session.StartedAt, shift);
                if (now < shiftEndUtc.Add(GracePeriod)) continue;

                session.EndedAt = now;
                closedCount++;
                _broadcaster.Publish(oasSlug, session.TenantId, "session.reminder", new { sessionId = session.Id, reason = "shift_ended" });
            }

            if (closedCount > 0)
            {
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("🏭 OAS-SESSION-WATCHDOG: auto-closed {Count} session(s) past shift end + grace on '{Slug}'", closedCount, oasSlug);
            }
        }
        catch (OasTenantNotProvisionedException)
        {
            // not yet schema-provisioned — skip
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🏭 OAS-SESSION-WATCHDOG: failed for tenant '{Slug}'", oasSlug);
        }
    }

    /// <summary>The shift's end timestamp on the same calendar day the session started, honoring midnight wraparound — the actual boundary the grace period is measured from (v15 fix).</summary>
    private static DateTimeOffset ShiftEndUtc(DateTimeOffset sessionStartedAt, OasShiftTemplate shift)
    {
        var startDate = sessionStartedAt.UtcDateTime.Date;
        var endDateTime = startDate.Add(shift.EndTime.ToTimeSpan());
        if (shift.CrossesMidnight) endDateTime = endDateTime.AddDays(1);
        return new DateTimeOffset(endDateTime, TimeSpan.Zero);
    }
}
