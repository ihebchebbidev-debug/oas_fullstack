using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Lookups.DTOs;
using MyApi.Modules.OAS.Lookups.Models;

namespace MyApi.Modules.OAS.Lookups.Services;

public interface IOasLookupService
{
    Task<IReadOnlyList<OasLookupValueDto>> GetByTypeAsync(int tenantId, string type);
    Task<OasLookupValueDto> CreateAsync(int tenantId, string type, OasLookupValueRequestDto request);
    Task<bool> UpdateAsync(int tenantId, string type, Guid id, OasLookupValueRequestDto request);
    Task<bool> DeleteAsync(int tenantId, string type, Guid id);
}

public class OasLookupService : IOasLookupService
{
    private readonly OasDbContext _db;
    public OasLookupService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasLookupValueDto>> GetByTypeAsync(int tenantId, string type)
    {
        var rows = await _db.Set<OasLookupValue>().Where(v => v.Type == type && v.ArchivedAt == null).OrderBy(v => v.SortOrder).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<OasLookupValueDto> CreateAsync(int tenantId, string type, OasLookupValueRequestDto request)
    {
        var value = new OasLookupValue
        {
            TenantId = tenantId, Type = type, Code = request.Code, Label = request.Label,
            Color = request.Color, SortOrder = request.SortOrder, IsDefault = request.IsDefault,
        };
        _db.Set<OasLookupValue>().Add(value);
        await _db.SaveChangesAsync();
        return ToDto(value);
    }

    public async Task<bool> UpdateAsync(int tenantId, string type, Guid id, OasLookupValueRequestDto request)
    {
        var value = await _db.Set<OasLookupValue>().FirstOrDefaultAsync(v => v.Id == id && v.Type == type);
        if (value is null) return false;
        value.Code = request.Code; value.Label = request.Label; value.Color = request.Color;
        value.SortOrder = request.SortOrder; value.IsDefault = request.IsDefault;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int tenantId, string type, Guid id)
    {
        var value = await _db.Set<OasLookupValue>().FirstOrDefaultAsync(v => v.Id == id && v.Type == type);
        if (value is null) return false;
        value.ArchivedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private static OasLookupValueDto ToDto(OasLookupValue v) => new()
    {
        Id = v.Id, Type = v.Type, Code = v.Code, Label = v.Label, Color = v.Color, SortOrder = v.SortOrder, IsDefault = v.IsDefault,
    };
}
