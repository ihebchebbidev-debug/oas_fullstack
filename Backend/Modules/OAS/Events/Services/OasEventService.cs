using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Causes.Models;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Common.Realtime;
using MyApi.Modules.OAS.Equipments.Models;
using MyApi.Modules.OAS.Events.DTOs;
using MyApi.Modules.OAS.Events.Models;
using MyApi.Modules.OAS.Hierarchy.Models;

namespace MyApi.Modules.OAS.Events.Services;

public interface IOasEventService
{
    Task<OasEventDto> CreateAsync(int tenantId, Guid userId, OasCreateEventRequestDto request);
    Task<IReadOnlyList<OasEventDto>> GetAsync(int tenantId, string? kind, string? stage, string? q, Guid? lineId, DateTimeOffset? from, DateTimeOffset? to);
    Task<OasEventDto?> GetOneAsync(int tenantId, Guid id);
    Task<OasEventDto?> GetByClientEventIdAsync(int tenantId, Guid clientEventId);
    Task<IReadOnlyList<OasEventTransitionDto>> GetTransitionsAsync(int tenantId, Guid id);

    Task<(bool success, string? error, OasEventDto? dto)> TakeAsync(int tenantId, Guid actorId, Guid id);
    Task<(bool success, string? error, OasEventDto? dto)> SetEtaAsync(int tenantId, Guid id, OasEtaRequestDto request);
    Task<(bool success, string? error, OasEventDto? dto)> ArriveAsync(int tenantId, Guid id);
    Task<(bool success, string? error, OasEventDto? dto)> AdvanceAsync(int tenantId, Guid actorId, Guid id);
    Task<(bool success, string? error, OasEventDto? dto)> AckAsync(int tenantId, Guid actorId, Guid id);
    Task<(bool success, string? error, OasEventDto? dto)> EscalateAsync(int tenantId, Guid id);
    Task<(bool success, string? error, OasEventDto? dto)> RequalifyAsync(int tenantId, Guid id, OasRequalifyRequestDto request);
    Task<(bool success, string? error, OasEventDto? dto)> DeclineAsync(int tenantId, Guid id);
    Task<(bool success, string? error, OasEventDto? dto)> CloseAsync(int tenantId, Guid actorId, Guid id, OasCloseEventRequestDto request);
}

/// <summary>
/// Event state machine (spec §6.1 Événements). v15 guards, all enforced
/// here rather than by the client: `closed` is fully terminal (409 on
/// every action); `resolved` blocks take/ack/arrive/advance (they no
/// longer make sense) but still allows close, escalate, decline, and a
/// deliberate requalify (an intentional reopening via one audited action,
/// not the silent one-line reopen the old client did). SLA elapsed-time
/// comparisons floor rather than round (v15) — see EscalationSweepHostedService.
/// Every mutation here is picked up automatically by the DB's
/// trg_oas_audit_oas_events trigger — no per-action audit call needed,
/// which also fixes the old client's "escalate never audits" gap by
/// construction (the trigger fires on the table, not the code path).
/// </summary>
public class OasEventService : IOasEventService
{
    private static readonly OasEventStatus[] Terminal = { OasEventStatus.closed };

    private readonly OasDbContext _db;
    private readonly IOasSseBroadcaster _broadcaster;
    private readonly string? _oasSlug;

    public OasEventService(OasDbContext db, IOasSseBroadcaster broadcaster, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _broadcaster = broadcaster;
        _oasSlug = httpContextAccessor.HttpContext?.Items["OasSlug"] as string;
    }

    private void Broadcast(int tenantId, string eventType, OasEventDto dto)
    {
        if (_oasSlug is not null) _broadcaster.Publish(_oasSlug, tenantId, eventType, dto);
    }

    public async Task<OasEventDto> CreateAsync(int tenantId, Guid userId, OasCreateEventRequestDto request)
    {
        var existing = await _db.Set<OasEvent>().FirstOrDefaultAsync(e => e.ClientEventId == request.ClientEventId);
        if (existing is not null) return ToDto(existing);

        var post = await _db.Set<OasPost>().FindAsync(request.PostId);
        var line = post is null ? null : await _db.Set<OasLine>().FindAsync(post.LineId);
        var zone = line is null ? null : await _db.Set<OasZone>().FindAsync(line.ZoneId);

        var ev = new OasEvent
        {
            TenantId = tenantId, ClientEventId = request.ClientEventId,
            EventType = Enum.Parse<OasEventType>(request.EventType, true), Status = OasEventStatus.declared,
            PostId = request.PostId, LineId = line?.Id, ZoneId = zone?.Id, SiteId = zone?.SiteId,
            PostSessionId = request.PostSessionId, ProductionOrderId = request.ProductionOrderId,
            ProductId = request.ProductId, EquipmentId = request.EquipmentId, CauseId = request.CauseId,
            Criticality = string.IsNullOrEmpty(request.Criticality) ? OasCriticality.medium : Enum.Parse<OasCriticality>(request.Criticality, true),
            DeclaredBy = userId, DeclaredAt = request.DeclaredAt, Note = request.Note,
            // SlaMinutes/SlaDueAt are finalized by the trg_oas_events_sla
            // BEFORE INSERT trigger (spec §3, db trigger looks up
            // oas_sla_rules/oas_routing_rules) — this default is overwritten.
            SlaMinutes = 10,
        };
        _db.Set<OasEvent>().Add(ev);
        await _db.SaveChangesAsync();
        await _db.Entry(ev).ReloadAsync(); // pick up trigger-computed sla_due_at
        var createdDto = ToDto(ev);
        Broadcast(tenantId, "event.created", createdDto);
        return createdDto;
    }

    public async Task<IReadOnlyList<OasEventDto>> GetAsync(int tenantId, string? kind, string? stage, string? q, Guid? lineId, DateTimeOffset? from, DateTimeOffset? to)
    {
        var query = _db.Set<OasEvent>().AsQueryable();
        if (!string.IsNullOrEmpty(kind) && Enum.TryParse<OasEventType>(kind, true, out var kindEnum)) query = query.Where(e => e.EventType == kindEnum);
        if (!string.IsNullOrEmpty(stage) && Enum.TryParse<OasEventStatus>(stage, true, out var stageEnum)) query = query.Where(e => e.Status == stageEnum);
        if (lineId is not null) query = query.Where(e => e.LineId == lineId);
        if (from is not null) query = query.Where(e => e.DeclaredAt >= from);
        if (to is not null) query = query.Where(e => e.DeclaredAt <= to);

        var rows = await query.OrderByDescending(e => e.DeclaredAt).ToListAsync();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim().ToLowerInvariant();
            rows = rows.Where(e => (e.Note ?? "").ToLowerInvariant().Contains(needle)).ToList();
        }

        return rows.Select(ToDto).ToList();
    }

    public async Task<OasEventDto?> GetOneAsync(int tenantId, Guid id)
    {
        var ev = await _db.Set<OasEvent>().FindAsync(id);
        return ev is null ? null : ToDto(ev);
    }

    public async Task<OasEventDto?> GetByClientEventIdAsync(int tenantId, Guid clientEventId)
    {
        var ev = await _db.Set<OasEvent>().FirstOrDefaultAsync(e => e.ClientEventId == clientEventId);
        return ev is null ? null : ToDto(ev);
    }

    public async Task<IReadOnlyList<OasEventTransitionDto>> GetTransitionsAsync(int tenantId, Guid id)
    {
        var rows = await _db.Set<OasEventTransition>().Where(t => t.EventId == id).OrderBy(t => t.OccurredAt).ToListAsync();
        return rows.Select(t => new OasEventTransitionDto { FromStatus = t.FromStatus?.ToString(), ToStatus = t.ToStatus.ToString(), ActorId = t.ActorId, OccurredAt = t.OccurredAt }).ToList();
    }

    public async Task<(bool success, string? error, OasEventDto? dto)> TakeAsync(int tenantId, Guid actorId, Guid id)
    {
        var (ev, error) = await LoadForMutationAsync(id, allowFromResolved: false);
        if (ev is null) return (false, error, null);

        ev.AssigneeId = actorId;
        if (ev.Status is OasEventStatus.declared or OasEventStatus.notified) ev.Status = OasEventStatus.acknowledged;
        ev.AcknowledgedAt ??= DateTimeOffset.UtcNow;
        ev.AcknowledgedBy ??= actorId;
        await _db.SaveChangesAsync();
        var updatedDto = ToDto(ev);
        Broadcast(tenantId, "event.updated", updatedDto);
        return (true, null, updatedDto);
    }

    public async Task<(bool success, string? error, OasEventDto? dto)> SetEtaAsync(int tenantId, Guid id, OasEtaRequestDto request)
    {
        var (ev, error) = await LoadForMutationAsync(id, allowFromResolved: true);
        if (ev is null) return (false, error, null);
        ev.EtaMinutes = request.EtaMinutes;
        await _db.SaveChangesAsync();
        var updatedDto = ToDto(ev);
        Broadcast(tenantId, "event.updated", updatedDto);
        return (true, null, updatedDto);
    }

    public async Task<(bool success, string? error, OasEventDto? dto)> ArriveAsync(int tenantId, Guid id)
    {
        var (ev, error) = await LoadForMutationAsync(id, allowFromResolved: false);
        if (ev is null) return (false, error, null);
        ev.OnSiteAt = DateTimeOffset.UtcNow;
        ev.Status = OasEventStatus.on_site;
        await _db.SaveChangesAsync();
        var updatedDto = ToDto(ev);
        Broadcast(tenantId, "event.updated", updatedDto);
        return (true, null, updatedDto);
    }

    public async Task<(bool success, string? error, OasEventDto? dto)> AdvanceAsync(int tenantId, Guid actorId, Guid id)
    {
        var (ev, error) = await LoadForMutationAsync(id, allowFromResolved: false);
        if (ev is null) return (false, error, null);

        ev.Status = OasEventStatus.resolved;
        ev.ResolvedAt = DateTimeOffset.UtcNow;
        ev.ResolvedBy = actorId;
        await _db.SaveChangesAsync();
        var updatedDto = ToDto(ev);
        Broadcast(tenantId, "event.updated", updatedDto);
        return (true, null, updatedDto);
    }

    public async Task<(bool success, string? error, OasEventDto? dto)> AckAsync(int tenantId, Guid actorId, Guid id)
    {
        var (ev, error) = await LoadForMutationAsync(id, allowFromResolved: false);
        if (ev is null) return (false, error, null);

        ev.AcknowledgedAt = DateTimeOffset.UtcNow;
        ev.AcknowledgedBy = actorId;
        if (ev.Status is OasEventStatus.declared or OasEventStatus.notified) ev.Status = OasEventStatus.acknowledged;
        await _db.SaveChangesAsync();
        var updatedDto = ToDto(ev);
        Broadcast(tenantId, "event.updated", updatedDto);
        return (true, null, updatedDto);
    }

    public async Task<(bool success, string? error, OasEventDto? dto)> EscalateAsync(int tenantId, Guid id)
    {
        // Escalate is allowed even from resolved (an exceptional re-alert)
        // — only `closed` blocks it.
        var (ev, error) = await LoadForMutationAsync(id, allowFromResolved: true);
        if (ev is null) return (false, error, null);

        ev.EscalationLevel = ev.EscalationLevel switch
        {
            OasEscalationLevel.none => OasEscalationLevel.level_1,
            OasEscalationLevel.level_1 => OasEscalationLevel.level_2,
            _ => ev.EscalationLevel, // already level_2 — never regresses, never re-escalates past max
        };
        await _db.SaveChangesAsync();
        var updatedDto = ToDto(ev);
        Broadcast(tenantId, "event.updated", updatedDto);
        return (true, null, updatedDto);
    }

    public async Task<(bool success, string? error, OasEventDto? dto)> RequalifyAsync(int tenantId, Guid id, OasRequalifyRequestDto request)
    {
        var (ev, error) = await LoadForMutationAsync(id, allowFromResolved: true); // deliberate reopen, allowed even from resolved
        if (ev is null) return (false, error, null);

        ev.EventType = Enum.Parse<OasEventType>(request.EventType, true);
        ev.Status = OasEventStatus.notified;
        await _db.SaveChangesAsync();
        var updatedDto = ToDto(ev);
        Broadcast(tenantId, "event.updated", updatedDto);
        return (true, null, updatedDto);
    }

    /// <summary>v15: applies the SAME effect as escalate — the old client's `declineEvent()` did both in one call, and the server preserves that as one action rather than requiring the client to chain /decline then /escalate.</summary>
    public async Task<(bool success, string? error, OasEventDto? dto)> DeclineAsync(int tenantId, Guid id)
    {
        var (ev, error) = await LoadForMutationAsync(id, allowFromResolved: true);
        if (ev is null) return (false, error, null);

        ev.AssigneeId = null;
        ev.EscalationLevel = ev.EscalationLevel switch
        {
            OasEscalationLevel.none => OasEscalationLevel.level_1,
            OasEscalationLevel.level_1 => OasEscalationLevel.level_2,
            _ => ev.EscalationLevel,
        };
        await _db.SaveChangesAsync();
        var updatedDto = ToDto(ev);
        Broadcast(tenantId, "event.updated", updatedDto);
        return (true, null, updatedDto);
    }

    public async Task<(bool success, string? error, OasEventDto? dto)> CloseAsync(int tenantId, Guid actorId, Guid id, OasCloseEventRequestDto request)
    {
        var ev = await _db.Set<OasEvent>().FindAsync(id);
        if (ev is null) return (false, "not_found", null);
        if (ev.Status == OasEventStatus.closed) return (true, null, ToDto(ev)); // idempotent

        ev.Status = OasEventStatus.closed;
        ev.ClosureType = Enum.Parse<OasClosureType>(request.ClosureType, true);
        ev.ClosureNote = request.Note;
        ev.ClosedAt = DateTimeOffset.UtcNow;
        ev.ClosedBy = actorId;
        await _db.SaveChangesAsync();
        var updatedDto = ToDto(ev);
        Broadcast(tenantId, "event.updated", updatedDto);
        return (true, null, updatedDto);
    }

    /// <summary>Central 409 guard (spec v15): `closed` blocks every action. `resolved` additionally blocks take/ack/arrive/advance unless the caller explicitly says those still make sense (none currently do, so allowFromResolved gates the rest).</summary>
    private async Task<(OasEvent? ev, string? error)> LoadForMutationAsync(Guid id, bool allowFromResolved)
    {
        var ev = await _db.Set<OasEvent>().FindAsync(id);
        if (ev is null) return (null, "not_found");
        if (Terminal.Contains(ev.Status)) return (null, "event_closed");
        if (!allowFromResolved && ev.Status == OasEventStatus.resolved) return (null, "event_already_resolved");
        return (ev, null);
    }

    private static OasEventDto ToDto(OasEvent e) => new()
    {
        Id = e.Id, EventType = e.EventType.ToString(), Status = e.Status.ToString(), PostId = e.PostId, LineId = e.LineId,
        CauseId = e.CauseId, Criticality = e.Criticality.ToString(), DeclaredBy = e.DeclaredBy, DeclaredAt = e.DeclaredAt,
        AssigneeId = e.AssigneeId, EtaMinutes = e.EtaMinutes, SlaMinutes = e.SlaMinutes, SlaDueAt = e.SlaDueAt,
        SlaBreached = e.SlaBreached, EscalationLevel = e.EscalationLevel.ToString(), ClosedAt = e.ClosedAt,
    };
}
