using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Hierarchy.Models;
using MyApi.Modules.OAS.Kpi.DTOs;
using MyApi.Modules.OAS.Kpi.Models;

namespace MyApi.Modules.OAS.Kpi.Services;

public interface IOasKpiService
{
    Task<OasKpiDailyDto> GetDailyAsync(int tenantId, Guid? postId, Guid? lineId, DateOnly from, DateOnly to);
    Task<IReadOnlyList<OasParetoEntryDto>> GetParetoAsync(int tenantId, Guid? postId, DateOnly from, DateOnly to);
    Task<IReadOnlyList<OasTrendPointDto>> GetTrendAsync(int tenantId, Guid? postId, Guid? lineId, DateOnly from, DateOnly to);
    Task<IReadOnlyList<OasLineComparisonEntryDto>> GetLineComparisonAsync(int tenantId, DateOnly from, DateOnly to);
    Task<IReadOnlyList<OasSlaSummaryEntryDto>> GetSlaSummaryAsync(int tenantId, DateOnly from, DateOnly to);
    Task<IReadOnlyList<OasCadenceGapEntryDto>> GetCadenceGapAsync(int tenantId, DateOnly from, DateOnly to);

    Task<OasAndonMessageDto?> GetAndonMessageAsync(int tenantId, Guid? lineId);
    Task<OasAndonMessageDto> SetAndonMessageAsync(int tenantId, Guid? actorId, OasAndonMessageRequestDto request);
}

/// <summary>
/// Server-side ports of `liveState.ts` (spec §7.3) — computed live from raw
/// declarations/events/sessions rather than a precomputed table, so parity
/// with the client's exact formulas is the ONLY source of truth, not a
/// second copy that can drift. v15 fixes applied here, not carried over
/// from the buggy client: OpeningMin resolves from a real shift template
/// (never a hardcoded 480 unless nothing is configured — see
/// `ResolveOpeningMinAsync`); MTTR is 0 (not 1) when there are zero stops;
/// line comparison computes a real aggregate, never blends with a static
/// demo seed.
/// </summary>
public class OasKpiService : IOasKpiService
{
    private const int DefaultOpeningMin = 480;
    private const decimal DefaultCadence = 60;

    private readonly OasDbContext _db;
    public OasKpiService(OasDbContext db) => _db = db;

    public async Task<OasKpiDailyDto> GetDailyAsync(int tenantId, Guid? postId, Guid? lineId, DateOnly from, DateOnly to)
    {
        var (fromTs, toTs) = ToRange(from, to);

        var stopRows = await _db.Database.SqlQueryRaw<StopAgg>(
            """
            select coalesce(sum(coalesce(duration_sec, extract(epoch from (coalesce(closed_at, now()) - declared_at))::int)), 0)::int as "StopSeconds",
                   count(*)::int as "StopsCount"
            from oas_events
            where tenant_id = {0}
              and ({1}::uuid is null or post_id = {1})
              and ({2}::uuid is null or line_id = {2})
              and declared_at >= {3} and declared_at < {4}
              and status = 'closed'
            """, tenantId, postId, lineId, fromTs, toTs).FirstOrDefaultAsync() ?? new StopAgg();

        var declRows = await _db.Database.SqlQueryRaw<DeclAgg>(
            """
            select coalesce(sum(quantity_ok), 0) as "Ok", coalesce(sum(quantity_nok), 0) as "Nok"
            from oas_declarations
            where tenant_id = {0}
              and ({1}::uuid is null or post_id = {1})
              and occurred_at >= {2} and occurred_at < {3}
            """, tenantId, postId, fromTs, toTs).FirstOrDefaultAsync() ?? new DeclAgg();

        var openingMin = await ResolveOpeningMinAsync(tenantId, postId, lineId, from);
        var (cadence, cadenceKnown) = await ResolveCadenceAsync(tenantId, postId);

        var stopMinutes = stopRows.StopSeconds / 60;
        var availability = Clamp0100(((double)(openingMin - stopMinutes) / openingMin) * 100);
        var totalQty = declRows.Ok + declRows.Nok;
        var quality = Clamp0100((double)(declRows.Ok / Math.Max(1, totalQty)) * 100);

        double? performance = null;
        if (cadenceKnown)
        {
            var runHours = Math.Max(0, openingMin - stopMinutes) / 60.0;
            var theoretical = Math.Max(1, (decimal)runHours * cadence);
            performance = Clamp0100((double)(totalQty / theoretical) * 100);
        }

        double? oee = performance is null ? null : Clamp0100((availability * performance.Value * quality) / 10_000);

        return new OasKpiDailyDto
        {
            OpeningMin = openingMin, Availability = availability, Quality = quality, Performance = performance,
            CadenceKnown = cadenceKnown, Oee = oee, StopMinutes = stopMinutes, StopsCount = stopRows.StopsCount,
            // v15: 0 stops → 0 MTTR, not 1 (the old client's `max(1, …)` guard produced a false "1 min" for a line with no stops at all).
            Mttr = stopRows.StopsCount == 0 ? 0 : Math.Max(1, (int)Math.Round((double)stopMinutes / stopRows.StopsCount)),
            ProducedOk = declRows.Ok, ProducedNok = declRows.Nok,
        };
    }

    public async Task<IReadOnlyList<OasParetoEntryDto>> GetParetoAsync(int tenantId, Guid? postId, DateOnly from, DateOnly to)
    {
        var (fromTs, toTs) = ToRange(from, to);
        var rows = await _db.Database.SqlQueryRaw<ParetoRow>(
            """
            select cause_id as "CauseId",
                   coalesce(sum(greatest(1, round(extract(epoch from (coalesce(closed_at, now()) - declared_at)) / 60)))::int, 0) as "LostMinutes"
            from oas_events
            where tenant_id = {0}
              and ({1}::uuid is null or post_id = {1})
              and declared_at >= {2} and declared_at < {3}
              and status = 'closed'
            group by cause_id
            order by "LostMinutes" desc
            """, tenantId, postId, fromTs, toTs).ToListAsync();

        return rows.Select(r => new OasParetoEntryDto { CauseId = r.CauseId, LostMinutes = r.LostMinutes }).ToList();
    }

    public async Task<IReadOnlyList<OasTrendPointDto>> GetTrendAsync(int tenantId, Guid? postId, Guid? lineId, DateOnly from, DateOnly to)
    {
        var points = new List<OasTrendPointDto>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var daily = await GetDailyAsync(tenantId, postId, lineId, d, d);
            points.Add(new OasTrendPointDto { Date = d, Oee = daily.Oee });
        }
        return points;
    }

    /// <summary>v15: a real per-line aggregate — no 50/50 blend with a static seed value (the old client's `(lineSeedTrs + liveSessionTrs) / 2` let a 2-minute-old session swing the whole line's reported TRS halfway).</summary>
    public async Task<IReadOnlyList<OasLineComparisonEntryDto>> GetLineComparisonAsync(int tenantId, DateOnly from, DateOnly to)
    {
        var lines = await _db.Set<OasLine>().ToListAsync();
        var results = new List<OasLineComparisonEntryDto>();
        var days = Math.Max(1, to.DayNumber - from.DayNumber + 1);

        foreach (var line in lines)
        {
            var daily = await GetDailyAsync(tenantId, postId: null, lineId: line.Id, from, to);
            var (fromTs, toTs) = ToRange(from, to);
            var scrapAgg = await _db.Database.SqlQueryRaw<DeclAgg>(
                """
                select coalesce(sum(quantity_ok), 0) as "Ok", coalesce(sum(quantity_nok), 0) as "Nok"
                from oas_declarations d join oas_posts p on p.id = d.post_id
                where d.tenant_id = {0} and p.line_id = {1} and d.occurred_at >= {2} and d.occurred_at < {3}
                """, tenantId, line.Id, fromTs, toTs).FirstOrDefaultAsync() ?? new DeclAgg();

            var total = scrapAgg.Ok + scrapAgg.Nok;
            var scrap = total == 0 ? 0 : Math.Round((double)(scrapAgg.Nok / total) * 100, 1);

            // Same MTBF-lite formula as the daily tile (opening time over the
            // period, minus lost time, spread over the stops) — computed here
            // so Reports.tsx never re-derives it client-side (spec §12 rule 10).
            var mtbfMin = daily.StopsCount == 0 ? 0 : Math.Max(0, (int)Math.Round((double)(daily.OpeningMin * days - daily.StopMinutes) / daily.StopsCount));

            results.Add(new OasLineComparisonEntryDto { LineId = line.Id, Trs = Math.Round(daily.Oee ?? 0, 0), Scrap = scrap, StopsCount = daily.StopsCount, MtbfMin = mtbfMin });
        }
        return results;
    }

    public async Task<IReadOnlyList<OasSlaSummaryEntryDto>> GetSlaSummaryAsync(int tenantId, DateOnly from, DateOnly to)
    {
        var (fromTs, toTs) = ToRange(from, to);
        var rows = await _db.Database.SqlQueryRaw<SlaRow>(
            """
            select event_type::text as "EventType",
                   count(*)::int as "Total",
                   count(*) filter (where extract(epoch from (coalesce(closed_at, now()) - declared_at)) / 60 <= sla_minutes)::int as "OnTime"
            from oas_events
            where tenant_id = {0} and declared_at >= {1} and declared_at < {2}
            group by event_type
            """, tenantId, fromTs, toTs).ToListAsync();

        return rows.Select(r => new OasSlaSummaryEntryDto
        {
            EventType = r.EventType, Total = r.Total, OnTime = r.OnTime,
            OnTimeRatio = r.Total == 0 ? 0 : Math.Round((double)r.OnTime / r.Total * 100, 1),
        }).ToList();
    }

    public async Task<IReadOnlyList<OasCadenceGapEntryDto>> GetCadenceGapAsync(int tenantId, DateOnly from, DateOnly to)
    {
        var (fromTs, toTs) = ToRange(from, to);
        var posts = await _db.Set<OasPost>().Where(p => p.IsActive).ToListAsync();
        var results = new List<OasCadenceGapEntryDto>();

        foreach (var post in posts)
        {
            var declAgg = await _db.Database.SqlQueryRaw<DeclAgg>(
                """select coalesce(sum(quantity_ok), 0) as "Ok", coalesce(sum(quantity_nok), 0) as "Nok" from oas_declarations where tenant_id = {0} and post_id = {1} and occurred_at >= {2} and occurred_at < {3}""",
                tenantId, post.Id, fromTs, toTs).FirstOrDefaultAsync() ?? new DeclAgg();
            var actual = declAgg.Ok + declAgg.Nok;
            if (actual == 0) continue;

            var (cadence, cadenceKnown) = await ResolveCadenceAsync(tenantId, post.Id);
            if (!cadenceKnown) continue;

            var openingMin = await ResolveOpeningMinAsync(tenantId, post.Id, null, from);
            var theoretical = Math.Max(1, (decimal)(openingMin / 60.0) * cadence);
            var gap = (double)((actual - theoretical) / theoretical) * 100;

            results.Add(new OasCadenceGapEntryDto { PostId = post.Id, ActualQty = actual, TheoreticalQty = theoretical, GapPercent = Math.Round(gap, 1) });
        }
        return results;
    }

    public async Task<OasAndonMessageDto?> GetAndonMessageAsync(int tenantId, Guid? lineId)
    {
        var msg = await _db.Set<OasAndonMessage>().FirstOrDefaultAsync(m => m.LineId == lineId);
        return msg is null ? null : new OasAndonMessageDto { LineId = msg.LineId, Message = msg.Message };
    }

    public async Task<OasAndonMessageDto> SetAndonMessageAsync(int tenantId, Guid? actorId, OasAndonMessageRequestDto request)
    {
        var msg = await _db.Set<OasAndonMessage>().FirstOrDefaultAsync(m => m.LineId == request.LineId);
        if (msg is null)
        {
            msg = new OasAndonMessage { TenantId = tenantId, LineId = request.LineId };
            _db.Set<OasAndonMessage>().Add(msg);
        }
        msg.Message = request.Message;
        msg.UpdatedBy = actorId;
        msg.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return new OasAndonMessageDto { LineId = msg.LineId, Message = msg.Message };
    }

    /// <summary>
    /// v15: resolves the REAL configured shift's opening minutes — 480 is a
    /// fallback only if nothing is configured for this post's site, never a
    /// blanket assumption. When called for a line-level aggregate (no
    /// specific post — e.g. `GetLineComparisonAsync`), falls back to any
    /// post on that line with a session on the date, so a line's real MTBF
    /// isn't silently laundered through the plant-wide default either.
    /// </summary>
    private async Task<int> ResolveOpeningMinAsync(int tenantId, Guid? postId, Guid? lineId, DateOnly date)
    {
        if (postId is null && lineId is null) return DefaultOpeningMin;

        var minutes = await _db.Database.SqlQueryRaw<int?>(
            """
            select case when st.crosses_midnight
                        then (extract(epoch from (st.end_time - st.start_time)) / 60 + 1440)::int - st.break_minutes
                        else (extract(epoch from (st.end_time - st.start_time)) / 60)::int - st.break_minutes
                   end as "Value"
            from oas_post_sessions ps
            join oas_shift_templates st on st.id = ps.shift_template_id
            join oas_posts p on p.id = ps.post_id
            where ({0}::uuid is null or ps.post_id = {0})
              and ({1}::uuid is null or p.line_id = {1})
              and ps.started_at::date = {2}
            order by ps.started_at desc
            limit 1
            """, postId, lineId, DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)).FirstOrDefaultAsync();

        return minutes is > 0 ? minutes.Value : DefaultOpeningMin;
    }

    private async Task<(decimal rate, bool known)> ResolveCadenceAsync(int tenantId, Guid? postId)
    {
        if (postId is null) return (DefaultCadence, false);
        var rate = await _db.Database.SqlQueryRaw<decimal?>(
            """select rate as "Value" from oas_routings where post_id = {0} order by updated_at desc limit 1""", postId)
            .FirstOrDefaultAsync();
        return rate is > 0 ? (rate.Value, true) : (DefaultCadence, false);
    }

    private static double Clamp0100(double value) => Math.Clamp(value, 0, 100);

    // DateOnly.ToDateTime always returns DateTimeKind.Unspecified — Npgsql's
    // EF Core provider refuses to bind that to a `timestamptz` parameter
    // ("Cannot write DateTime with Kind=Unspecified... only UTC is
    // supported"), so every raw-SQL call below using this range would throw
    // on every request. Confirmed by direct reproduction against a real
    // database with the exact production package versions.
    private static (DateTime from, DateTime to) ToRange(DateOnly from, DateOnly to)
        => (DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));

    private sealed class StopAgg { public int StopSeconds { get; set; } public int StopsCount { get; set; } }
    private sealed class DeclAgg { public decimal Ok { get; set; } public decimal Nok { get; set; } }
    private sealed class ParetoRow { public Guid? CauseId { get; set; } public int LostMinutes { get; set; } }
    private sealed class SlaRow { public string EventType { get; set; } = string.Empty; public int Total { get; set; } public int OnTime { get; set; } }
}
