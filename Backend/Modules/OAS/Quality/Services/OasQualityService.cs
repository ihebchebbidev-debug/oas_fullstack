using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Quality.DTOs;
using MyApi.Modules.OAS.Quality.Models;

namespace MyApi.Modules.OAS.Quality.Services;

public interface IOasQualityService
{
    Task<OasQualityCheckDto> CreateCheckAsync(int tenantId, Guid inspectorId, OasQualityCheckRequestDto request);
    Task<IReadOnlyList<OasQualityCheckDto>> GetChecksAsync(int tenantId, Guid? postId);

    Task<IReadOnlyList<OasQualityCheckTemplateDto>> GetTemplatesAsync(int tenantId);
    Task<OasQualityCheckTemplateDto> CreateTemplateAsync(int tenantId, OasQualityCheckTemplateRequestDto request);
    Task<bool> UpdateTemplateAsync(int tenantId, Guid id, OasQualityCheckTemplateRequestDto request);
    Task<bool> DeleteTemplateAsync(int tenantId, Guid id);

    Task<IReadOnlyList<OasQualityCheckTemplateItemDto>> GetTemplateItemsAsync(int tenantId, Guid templateId);
    Task PutTemplateItemsAsync(int tenantId, Guid templateId, IReadOnlyList<OasQualityCheckTemplateItemRequestDto> items);
}

public class OasQualityService : IOasQualityService
{
    private readonly OasDbContext _db;
    public OasQualityService(OasDbContext db) => _db = db;

    public async Task<OasQualityCheckDto> CreateCheckAsync(int tenantId, Guid inspectorId, OasQualityCheckRequestDto request)
    {
        var existing = await _db.Set<OasQualityCheck>().FirstOrDefaultAsync(c => c.ClientEventId == request.ClientEventId);
        if (existing is not null) return ToDto(existing);

        var check = new OasQualityCheck
        {
            TenantId = tenantId, ClientEventId = request.ClientEventId, PostId = request.PostId,
            ProductionOrderId = request.ProductionOrderId, ProductId = request.ProductId, ChangeoverId = request.ChangeoverId,
            TemplateId = request.TemplateId, CheckType = Enum.Parse<OasCheckType>(request.CheckType, true),
            Result = Enum.Parse<OasCheckResult>(request.Result, true), QuantityChecked = request.QuantityChecked,
            QuantityRejected = request.QuantityRejected, CauseId = request.CauseId, Note = request.Note,
            OccurredAt = request.OccurredAt, InspectorId = inspectorId,
        };
        _db.Set<OasQualityCheck>().Add(check);
        await _db.SaveChangesAsync();
        return ToDto(check);
    }

    public async Task<IReadOnlyList<OasQualityCheckDto>> GetChecksAsync(int tenantId, Guid? postId)
    {
        var q = _db.Set<OasQualityCheck>().AsQueryable();
        if (postId is not null) q = q.Where(c => c.PostId == postId);
        var rows = await q.OrderByDescending(c => c.OccurredAt).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<OasQualityCheckTemplateDto>> GetTemplatesAsync(int tenantId)
    {
        var rows = await _db.Set<OasQualityCheckTemplate>().Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
        return rows.Select(ToTemplateDto).ToList();
    }

    public async Task<OasQualityCheckTemplateDto> CreateTemplateAsync(int tenantId, OasQualityCheckTemplateRequestDto request)
    {
        var template = new OasQualityCheckTemplate
        {
            TenantId = tenantId, Code = request.Code, Name = request.Name, CheckType = Enum.Parse<OasCheckType>(request.CheckType, true),
        };
        _db.Set<OasQualityCheckTemplate>().Add(template);
        await _db.SaveChangesAsync();
        return ToTemplateDto(template);
    }

    public async Task<bool> UpdateTemplateAsync(int tenantId, Guid id, OasQualityCheckTemplateRequestDto request)
    {
        var template = await _db.Set<OasQualityCheckTemplate>().FindAsync(id);
        if (template is null) return false;
        template.Code = request.Code; template.Name = request.Name; template.CheckType = Enum.Parse<OasCheckType>(request.CheckType, true);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteTemplateAsync(int tenantId, Guid id)
    {
        var template = await _db.Set<OasQualityCheckTemplate>().FindAsync(id);
        if (template is null) return false;
        template.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<OasQualityCheckTemplateItemDto>> GetTemplateItemsAsync(int tenantId, Guid templateId)
    {
        var rows = await _db.Set<OasQualityCheckTemplateItem>().Where(i => i.TemplateId == templateId).OrderBy(i => i.SortOrder).ToListAsync();
        return rows.Select(ToItemDto).ToList();
    }

    public async Task PutTemplateItemsAsync(int tenantId, Guid templateId, IReadOnlyList<OasQualityCheckTemplateItemRequestDto> items)
    {
        var existing = await _db.Set<OasQualityCheckTemplateItem>().Where(i => i.TemplateId == templateId).ToListAsync();
        _db.Set<OasQualityCheckTemplateItem>().RemoveRange(existing);

        foreach (var item in items)
        {
            _db.Set<OasQualityCheckTemplateItem>().Add(new OasQualityCheckTemplateItem
            {
                TenantId = tenantId, TemplateId = templateId, Label = item.Label, ValueType = item.ValueType,
                MinValue = item.MinValue, MaxValue = item.MaxValue, IsRequired = item.IsRequired, SortOrder = item.SortOrder,
            });
        }
        await _db.SaveChangesAsync();
    }

    private static OasQualityCheckDto ToDto(OasQualityCheck c) => new()
    {
        Id = c.Id, PostId = c.PostId, CheckType = c.CheckType.ToString(), Result = c.Result.ToString(),
        QuantityChecked = c.QuantityChecked, QuantityRejected = c.QuantityRejected, OccurredAt = c.OccurredAt,
    };

    private static OasQualityCheckTemplateDto ToTemplateDto(OasQualityCheckTemplate t) => new()
    {
        Id = t.Id, Code = t.Code, Name = t.Name, CheckType = t.CheckType.ToString(), IsActive = t.IsActive,
    };

    private static OasQualityCheckTemplateItemDto ToItemDto(OasQualityCheckTemplateItem i) => new()
    {
        Id = i.Id, Label = i.Label, ValueType = i.ValueType, MinValue = i.MinValue, MaxValue = i.MaxValue,
        IsRequired = i.IsRequired, SortOrder = i.SortOrder,
    };
}
