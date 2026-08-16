using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.Dashboards.DTOs;
using MyApi.Modules.Dashboards.Models;

namespace MyApi.Modules.Dashboards.Services;

public class DashboardLayoutService : IDashboardLayoutService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DashboardLayoutService>? _logger;

    public DashboardLayoutService(ApplicationDbContext db, ILogger<DashboardLayoutService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    private static string NormalizeScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? "default" : scope!.Trim();

    private List<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json!) ?? new List<string>();
        }
        catch (Exception ex)
        {
            // Malformed JSON in the DB shouldn't wipe the user's layout
            // silently — log so ops can investigate.
            _logger?.LogWarning(ex, "DashboardLayout JSON parse failed, defaulting to empty list");
            return new List<string>();
        }
    }

    public async Task<DashboardLayoutDto> GetAsync(int userId, string scope, CancellationToken ct = default)
    {
        scope = NormalizeScope(scope);
        var row = await _db.Set<DashboardLayout>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Scope == scope, ct);

        return new DashboardLayoutDto
        {
            Scope = scope,
            Order = ParseList(row?.OrderJson),
            Hidden = ParseList(row?.HiddenJson),
        };
    }

    public async Task<DashboardLayoutDto> SaveAsync(int userId, SaveDashboardLayoutRequest req, CancellationToken ct = default)
    {
        var scope = NormalizeScope(req.Scope);
        var orderJson = JsonSerializer.Serialize(req.Order ?? new List<string>());
        var hiddenJson = JsonSerializer.Serialize(req.Hidden ?? new List<string>());

        var row = await _db.Set<DashboardLayout>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Scope == scope, ct);

        if (row == null)
        {
            row = new DashboardLayout
            {
                UserId = userId,
                Scope = scope,
                OrderJson = orderJson,
                HiddenJson = hiddenJson,
            };
            _db.Set<DashboardLayout>().Add(row);
        }
        else
        {
            row.OrderJson = orderJson;
            row.HiddenJson = hiddenJson;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return new DashboardLayoutDto
        {
            Scope = scope,
            Order = req.Order ?? new List<string>(),
            Hidden = req.Hidden ?? new List<string>(),
        };
    }

    public async Task<bool> ResetAsync(int userId, string scope, CancellationToken ct = default)
    {
        scope = NormalizeScope(scope);
        var row = await _db.Set<DashboardLayout>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Scope == scope, ct);
        if (row == null) return false;
        _db.Set<DashboardLayout>().Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
