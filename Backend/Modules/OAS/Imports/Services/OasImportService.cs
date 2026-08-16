using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Causes.Models;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Equipments.Models;
using MyApi.Modules.OAS.Imports.DTOs;
using MyApi.Modules.OAS.Imports.Models;
using MyApi.Modules.OAS.Products.Models;
using System.Text.Json;

namespace MyApi.Modules.OAS.Imports.Services;

public interface IOasImportService
{
    Task<IReadOnlyList<OasImportDto>> GetAllAsync(int tenantId);
    Task<OasImportDto?> GetOneAsync(int tenantId, Guid id);
    Task<IReadOnlyList<OasImportLineDto>> GetLinesAsync(int tenantId, Guid id);
    Task<(bool success, string? error, OasImportDto? dto)> CreateAsync(int tenantId, Guid? importedBy, OasImportCreateRequestDto request);
    Task<(bool success, string? error, OasImportDto? dto)> CommitAsync(int tenantId, Guid id);
}

/// <summary>
/// v12 decision: `commit` returns 400 for an unsupported `datasetType`
/// instead of silently accepting-and-doing-nothing like `ImportPanel.tsx`
/// does today for "posts"/"orders". Supported types are exactly the ones
/// with a real mapping below — nothing is accepted "for later".
/// </summary>
public class OasImportService : IOasImportService
{
    private static readonly HashSet<string> SupportedKinds = new(StringComparer.OrdinalIgnoreCase) { "products", "causes", "equipment" };

    private readonly OasDbContext _db;
    public OasImportService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasImportDto>> GetAllAsync(int tenantId)
    {
        var rows = await _db.Set<OasImport>().OrderByDescending(i => i.CreatedAt).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<OasImportDto?> GetOneAsync(int tenantId, Guid id)
    {
        var import = await _db.Set<OasImport>().FindAsync(id);
        return import is null ? null : ToDto(import);
    }

    public async Task<IReadOnlyList<OasImportLineDto>> GetLinesAsync(int tenantId, Guid id)
    {
        var rows = await _db.Set<OasImportLine>().Where(l => l.ImportId == id).OrderBy(l => l.RowNumber).ToListAsync();
        return rows.Select(l => new OasImportLineDto { RowNumber = l.RowNumber, Status = l.Status, Error = l.Error }).ToList();
    }

    public async Task<(bool success, string? error, OasImportDto? dto)> CreateAsync(int tenantId, Guid? importedBy, OasImportCreateRequestDto request)
    {
        if (!SupportedKinds.Contains(request.Kind))
        {
            return (false, $"unsupported_dataset_type: {request.Kind}", null);
        }

        var import = new OasImport { TenantId = tenantId, Kind = request.Kind, Status = OasImportStatus.pending, RowsTotal = request.Rows.Count, ImportedBy = importedBy };
        _db.Set<OasImport>().Add(import);
        await _db.SaveChangesAsync();

        var rowNumber = 0;
        foreach (var row in request.Rows)
        {
            rowNumber++;
            _db.Set<OasImportLine>().Add(new OasImportLine
            {
                TenantId = tenantId, ImportId = import.Id, RowNumber = rowNumber,
                Raw = JsonSerializer.Serialize(row), Status = "pending",
            });
        }
        await _db.SaveChangesAsync();

        return (true, null, ToDto(import));
    }

    public async Task<(bool success, string? error, OasImportDto? dto)> CommitAsync(int tenantId, Guid id)
    {
        var import = await _db.Set<OasImport>().FindAsync(id);
        if (import is null) return (false, "not_found", null);
        if (import.Status == OasImportStatus.committed) return (true, null, ToDto(import)); // idempotent

        if (!SupportedKinds.Contains(import.Kind))
        {
            return (false, $"unsupported_dataset_type: {import.Kind}", null);
        }

        var lines = await _db.Set<OasImportLine>().Where(l => l.ImportId == id).OrderBy(l => l.RowNumber).ToListAsync();
        var ok = 0; var errors = 0;

        foreach (var line in lines)
        {
            try
            {
                var row = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line.Raw) ?? new();
                switch (import.Kind.ToLowerInvariant())
                {
                    case "products": CommitProductRow(tenantId, row); break;
                    case "causes": CommitCauseRow(tenantId, row); break;
                    case "equipment": CommitEquipmentRow(tenantId, row); break;
                }
                line.Status = "ok";
                ok++;
            }
            catch (Exception ex)
            {
                line.Status = "error";
                line.Error = ex.Message;
                errors++;
            }
        }

        import.RowsOk = ok;
        import.RowsError = errors;
        import.Status = OasImportStatus.committed;
        import.CommittedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return (true, null, ToDto(import));
    }

    private static string RequireString(Dictionary<string, JsonElement> row, string key)
        => row.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new InvalidOperationException($"missing or invalid '{key}'");

    private static string? OptString(Dictionary<string, JsonElement> row, string key)
        => row.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private void CommitProductRow(int tenantId, Dictionary<string, JsonElement> row)
    {
        _db.Set<OasProduct>().Add(new OasProduct
        {
            TenantId = tenantId, Reference = RequireString(row, "reference"), Name = RequireString(row, "name"),
            Customer = OptString(row, "customer"), Unit = OptString(row, "unit") ?? "pcs",
        });
    }

    private void CommitCauseRow(int tenantId, Dictionary<string, JsonElement> row)
    {
        _db.Set<OasCause>().Add(new OasCause
        {
            TenantId = tenantId, Domain = Enum.Parse<OasCauseDomain>(RequireString(row, "domain"), true),
            Code = RequireString(row, "code"), LabelFr = RequireString(row, "labelFr"), LabelAr = OptString(row, "labelAr") ?? RequireString(row, "labelFr"),
        });
    }

    private void CommitEquipmentRow(int tenantId, Dictionary<string, JsonElement> row)
    {
        _db.Set<OasEquipment>().Add(new OasEquipment
        {
            TenantId = tenantId, Code = RequireString(row, "code"), Name = RequireString(row, "name"),
            SerialNumber = OptString(row, "serialNumber"), Manufacturer = OptString(row, "manufacturer"),
        });
    }

    private static OasImportDto ToDto(OasImport i) => new()
    {
        Id = i.Id, Kind = i.Kind, Status = i.Status.ToString(),
        RowsTotal = i.RowsTotal, RowsOk = i.RowsOk, RowsError = i.RowsError, CreatedAt = i.CreatedAt,
    };
}
