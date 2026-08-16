using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Audit.DTOs;
using MyApi.Modules.OAS.Audit.Models;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.ShopFloorAuth.Models;

namespace MyApi.Modules.OAS.Audit.Services;

public interface IOasAuditService
{
    Task<IReadOnlyList<OasAuditLogDto>> GetAsync(int tenantId, string? entity, DateTimeOffset? from, DateTimeOffset? to, string? actor, string? action, string? q);
}

/// <summary>Read-only (spec §6.1 Audit: "aucun POST" — the DB trigger is the only writer).</summary>
public class OasAuditService : IOasAuditService
{
    private readonly OasDbContext _db;
    public OasAuditService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasAuditLogDto>> GetAsync(int tenantId, string? entity, DateTimeOffset? from, DateTimeOffset? to, string? actor, string? action, string? q)
    {
        var query = _db.Set<OasAuditLog>().AsQueryable();

        if (!string.IsNullOrEmpty(entity)) query = query.Where(a => a.EntityTable == entity);
        if (from is not null) query = query.Where(a => a.OccurredAt >= from);
        if (to is not null) query = query.Where(a => a.OccurredAt <= to);
        if (!string.IsNullOrEmpty(action) && Enum.TryParse<OasAuditAction>(action, true, out var actionEnum)) query = query.Where(a => a.Action == actionEnum);

        if (!string.IsNullOrWhiteSpace(actor))
        {
            if (Guid.TryParse(actor, out var actorId))
            {
                query = query.Where(a => a.ActorId == actorId);
            }
            else
            {
                var matchingIds = await _db.Set<OasUser>()
                    .Where(u => u.DisplayName != null && u.DisplayName.ToLower().Contains(actor.ToLower()))
                    .Select(u => u.Id).ToListAsync();
                query = query.Where(a => a.ActorId != null && matchingIds.Contains(a.ActorId.Value));
            }
        }

        var rows = await query.OrderByDescending(a => a.OccurredAt).Take(500).ToListAsync();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim().ToLowerInvariant();
            rows = rows.Where(a =>
                a.EntityTable.ToLowerInvariant().Contains(needle)
                || (a.Reason ?? "").ToLowerInvariant().Contains(needle)).ToList();
        }

        return rows.Select(a => new OasAuditLogDto
        {
            Id = a.Id, EntityTable = a.EntityTable, EntityId = a.EntityId, Action = a.Action.ToString(),
            ActorId = a.ActorId, Reason = a.Reason, OccurredAt = a.OccurredAt,
        }).ToList();
    }
}
