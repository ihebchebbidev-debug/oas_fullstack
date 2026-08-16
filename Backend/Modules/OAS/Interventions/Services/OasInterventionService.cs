using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Events.DTOs;
using MyApi.Modules.OAS.Events.Services;
using MyApi.Modules.OAS.Interventions.DTOs;
using MyApi.Modules.OAS.Interventions.Models;

namespace MyApi.Modules.OAS.Interventions.Services;

public interface IOasInterventionService
{
    Task<IReadOnlyList<OasInterventionDto>> GetAllAsync(int tenantId);
    Task<IReadOnlyList<OasInterventionDto>> GetInboxAsync(int tenantId, Guid assigneeId);
    Task<OasInterventionDto> CreateAsync(int tenantId, OasCreateInterventionRequestDto request);
    Task<(bool success, string? error)> AssignAsync(int tenantId, Guid actorId, Guid id);
    Task<(bool success, string? error)> StartAsync(int tenantId, Guid id);
    Task<(bool success, string? error)> CloseAsync(int tenantId, Guid actorId, Guid id);
}

/// <summary>
/// §13 point 8: oas_interventions is a lightweight PROJECTION over
/// oas_events — assign/start/close are documented aliases onto
/// events.take/arrive/close so the state machine is never duplicated.
/// This service keeps the projection row in sync as a convenience for
/// the inbox view, but oas_events remains the source of truth.
/// </summary>
public class OasInterventionService : IOasInterventionService
{
    private readonly OasDbContext _db;
    private readonly IOasEventService _events;
    public OasInterventionService(OasDbContext db, IOasEventService events)
    {
        _db = db;
        _events = events;
    }

    public async Task<IReadOnlyList<OasInterventionDto>> GetAllAsync(int tenantId)
    {
        var rows = await _db.Set<OasIntervention>().OrderByDescending(i => i.CreatedAt).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<OasInterventionDto>> GetInboxAsync(int tenantId, Guid assigneeId)
    {
        var rows = await _db.Set<OasIntervention>().Where(i => i.AssigneeId == assigneeId && i.Status != "closed").OrderByDescending(i => i.CreatedAt).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<OasInterventionDto> CreateAsync(int tenantId, OasCreateInterventionRequestDto request)
    {
        var intervention = new OasIntervention { TenantId = tenantId, EventId = request.EventId, Status = "open" };
        _db.Set<OasIntervention>().Add(intervention);
        await _db.SaveChangesAsync();
        return ToDto(intervention);
    }

    public async Task<(bool success, string? error)> AssignAsync(int tenantId, Guid actorId, Guid id)
    {
        var intervention = await _db.Set<OasIntervention>().FindAsync(id);
        if (intervention is null) return (false, "not_found");

        var (success, error, _) = await _events.TakeAsync(tenantId, actorId, intervention.EventId);
        if (!success) return (false, error);

        intervention.AssigneeId = actorId;
        intervention.AssignedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool success, string? error)> StartAsync(int tenantId, Guid id)
    {
        var intervention = await _db.Set<OasIntervention>().FindAsync(id);
        if (intervention is null) return (false, "not_found");

        var (success, error, _) = await _events.ArriveAsync(tenantId, intervention.EventId);
        if (!success) return (false, error);

        intervention.Status = "in_progress";
        intervention.StartedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool success, string? error)> CloseAsync(int tenantId, Guid actorId, Guid id)
    {
        var intervention = await _db.Set<OasIntervention>().FindAsync(id);
        if (intervention is null) return (false, "not_found");

        var (success, error, _) = await _events.CloseAsync(tenantId, actorId, intervention.EventId, new OasCloseEventRequestDto { ClosureType = "resolved" });
        if (!success) return (false, error);

        intervention.Status = "closed";
        intervention.ClosedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private static OasInterventionDto ToDto(OasIntervention i) => new() { Id = i.Id, EventId = i.EventId, AssigneeId = i.AssigneeId, Status = i.Status };
}
