using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Cadences.DTOs;
using MyApi.Modules.OAS.Cadences.Models;
using MyApi.Modules.OAS.Common;

namespace MyApi.Modules.OAS.Cadences.Services;

public interface IOasCadenceService
{
    Task<IReadOnlyList<OasCadenceDto>> GetAllAsync(int tenantId);
    Task<OasCadenceDto> UpsertNewVersionAsync(int tenantId, Guid? actorId, OasCadenceRequestDto request);
    Task<bool> UpdateAsync(int tenantId, Guid id, OasCadenceRequestDto request);
    Task<bool> DeleteAsync(int tenantId, Guid id);
    Task<IReadOnlyList<OasCadenceVersionDto>> GetHistoryAsync(int tenantId, Guid id);
    Task<OasCadenceDto?> GetCurrentAsync(int tenantId, Guid postId, Guid productId);
}

/// <summary>Spec §6.1 Cadences: POST creates a NEW VERSION (never overwrites) — oas_routings always holds the latest, oas_routing_versions is the append-only history.</summary>
public class OasCadenceService : IOasCadenceService
{
    private readonly OasDbContext _db;
    public OasCadenceService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasCadenceDto>> GetAllAsync(int tenantId)
    {
        var rows = await _db.Set<OasRouting>().ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<OasCadenceDto> UpsertNewVersionAsync(int tenantId, Guid? actorId, OasCadenceRequestDto request)
    {
        var current = await _db.Set<OasRouting>().FirstOrDefaultAsync(r => r.ProductId == request.ProductId && r.PostId == request.PostId);
        var lastVersion = await _db.Set<OasRoutingVersion>()
            .Where(v => v.ProductId == request.ProductId && v.PostId == request.PostId)
            .OrderByDescending(v => v.Version)
            .Select(v => (int?)v.Version)
            .FirstOrDefaultAsync();

        _db.Set<OasRoutingVersion>().Add(new OasRoutingVersion
        {
            TenantId = tenantId, ProductId = request.ProductId, PostId = request.PostId,
            Rate = request.Rate, Version = (lastVersion ?? 0) + 1, CreatedBy = actorId,
        });

        if (current is null)
        {
            current = new OasRouting { TenantId = tenantId, ProductId = request.ProductId, PostId = request.PostId };
            _db.Set<OasRouting>().Add(current);
        }
        current.Rate = request.Rate;
        current.OperatorsRequired = request.OperatorsRequired;
        current.ChangeoverTargetMin = request.ChangeoverTargetMin;

        await _db.SaveChangesAsync();
        return ToDto(current);
    }

    public async Task<bool> UpdateAsync(int tenantId, Guid id, OasCadenceRequestDto request)
    {
        var routing = await _db.Set<OasRouting>().FindAsync(id);
        if (routing is null) return false;
        routing.Rate = request.Rate;
        routing.OperatorsRequired = request.OperatorsRequired;
        routing.ChangeoverTargetMin = request.ChangeoverTargetMin;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int tenantId, Guid id)
    {
        var routing = await _db.Set<OasRouting>().FindAsync(id);
        if (routing is null) return false;
        _db.Set<OasRouting>().Remove(routing);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<OasCadenceVersionDto>> GetHistoryAsync(int tenantId, Guid id)
    {
        var routing = await _db.Set<OasRouting>().FindAsync(id);
        if (routing is null) return Array.Empty<OasCadenceVersionDto>();

        var versions = await _db.Set<OasRoutingVersion>()
            .Where(v => v.ProductId == routing.ProductId && v.PostId == routing.PostId)
            .OrderByDescending(v => v.Version)
            .ToListAsync();
        return versions.Select(v => new OasCadenceVersionDto { Version = v.Version, Rate = v.Rate, Since = v.Since }).ToList();
    }

    public async Task<OasCadenceDto?> GetCurrentAsync(int tenantId, Guid postId, Guid productId)
    {
        var routing = await _db.Set<OasRouting>().FirstOrDefaultAsync(r => r.PostId == postId && r.ProductId == productId);
        return routing is null ? null : ToDto(routing);
    }

    private static OasCadenceDto ToDto(OasRouting r) => new()
    {
        Id = r.Id, ProductId = r.ProductId, PostId = r.PostId, Rate = r.Rate,
        OperatorsRequired = r.OperatorsRequired, ChangeoverTargetMin = r.ChangeoverTargetMin,
    };
}
