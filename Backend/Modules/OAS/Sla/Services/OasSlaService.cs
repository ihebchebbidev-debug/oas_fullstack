using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Causes.Models;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Equipments.Models;
using MyApi.Modules.OAS.Sla.DTOs;
using MyApi.Modules.OAS.Sla.Models;

namespace MyApi.Modules.OAS.Sla.Services;

public interface IOasSlaService
{
    Task<IReadOnlyList<OasSlaRuleDto>> GetRulesAsync(int tenantId);
    Task<OasSlaRuleDto> CreateRuleAsync(int tenantId, OasSlaRuleRequestDto request);
    Task<bool> UpdateRuleAsync(int tenantId, Guid id, OasSlaRuleRequestDto request);
    Task<bool> DeleteRuleAsync(int tenantId, Guid id);

    Task<IReadOnlyList<OasEscalationDto>> GetEscalationsAsync(int tenantId, Guid? eventId);
    Task<bool> AckEscalationAsync(int tenantId, Guid actorId, Guid id);

    Task<IReadOnlyList<OasResponderAvailabilityDto>> GetAvailabilityAsync(int tenantId);
    Task<bool> SetAvailabilityAsync(int tenantId, Guid profileId, OasResponderAvailabilityRequestDto request);
}

public class OasSlaService : IOasSlaService
{
    private readonly OasDbContext _db;
    public OasSlaService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasSlaRuleDto>> GetRulesAsync(int tenantId)
    {
        var rows = await _db.Set<OasSlaRule>().OrderByDescending(r => r.Priority).ToListAsync();
        return rows.Select(ToRuleDto).ToList();
    }

    public async Task<OasSlaRuleDto> CreateRuleAsync(int tenantId, OasSlaRuleRequestDto request)
    {
        var rule = new OasSlaRule
        {
            TenantId = tenantId, EventType = Enum.Parse<OasEventType>(request.EventType, true),
            Criticality = string.IsNullOrEmpty(request.Criticality) ? null : Enum.Parse<OasCriticality>(request.Criticality, true),
            LineId = request.LineId, TargetMin = request.TargetMin, Priority = request.Priority,
        };
        _db.Set<OasSlaRule>().Add(rule);
        await _db.SaveChangesAsync();
        return ToRuleDto(rule);
    }

    public async Task<bool> UpdateRuleAsync(int tenantId, Guid id, OasSlaRuleRequestDto request)
    {
        var rule = await _db.Set<OasSlaRule>().FindAsync(id);
        if (rule is null) return false;
        rule.EventType = Enum.Parse<OasEventType>(request.EventType, true);
        rule.Criticality = string.IsNullOrEmpty(request.Criticality) ? null : Enum.Parse<OasCriticality>(request.Criticality, true);
        rule.LineId = request.LineId; rule.TargetMin = request.TargetMin; rule.Priority = request.Priority;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRuleAsync(int tenantId, Guid id)
    {
        var rule = await _db.Set<OasSlaRule>().FindAsync(id);
        if (rule is null) return false;
        _db.Set<OasSlaRule>().Remove(rule);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<OasEscalationDto>> GetEscalationsAsync(int tenantId, Guid? eventId)
    {
        var q = _db.Set<OasEscalation>().AsQueryable();
        if (eventId is not null) q = q.Where(e => e.EventId == eventId);
        var rows = await q.OrderByDescending(e => e.TriggeredAt).ToListAsync();
        return rows.Select(e => new OasEscalationDto { Id = e.Id, EventId = e.EventId, Level = e.Level.ToString(), TriggeredAt = e.TriggeredAt, Reason = e.Reason, AcknowledgedAt = e.AcknowledgedAt }).ToList();
    }

    public async Task<bool> AckEscalationAsync(int tenantId, Guid actorId, Guid id)
    {
        var escalation = await _db.Set<OasEscalation>().FindAsync(id);
        if (escalation is null) return false;
        escalation.AcknowledgedAt = DateTimeOffset.UtcNow;
        escalation.AcknowledgedBy = actorId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<OasResponderAvailabilityDto>> GetAvailabilityAsync(int tenantId)
    {
        var rows = await _db.Set<OasResponderAvailability>().ToListAsync();
        return rows.Select(r => new OasResponderAvailabilityDto { ProfileId = r.ProfileId, Busy = r.Busy, Reason = r.Reason }).ToList();
    }

    /// <summary>v15: `profileId` is validated by the controller against [OasAuthorize] — a plain operator may only set their OWN availability, never someone else's (the old client had no such concept at all, using one shared hardcoded name).</summary>
    public async Task<bool> SetAvailabilityAsync(int tenantId, Guid profileId, OasResponderAvailabilityRequestDto request)
    {
        var entry = await _db.Set<OasResponderAvailability>().FirstOrDefaultAsync(r => r.ProfileId == profileId);
        if (entry is null)
        {
            entry = new OasResponderAvailability { TenantId = tenantId, ProfileId = profileId };
            _db.Set<OasResponderAvailability>().Add(entry);
        }
        entry.Busy = request.Busy;
        entry.Reason = request.Reason;
        entry.Since = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private static OasSlaRuleDto ToRuleDto(OasSlaRule r) => new()
    {
        Id = r.Id, EventType = r.EventType.ToString(), Criticality = r.Criticality?.ToString(),
        LineId = r.LineId, TargetMin = r.TargetMin, Priority = r.Priority, IsActive = r.IsActive,
    };
}
