using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Reporting.DTOs;
using MyApi.Modules.Reporting.Models;

namespace MyApi.Modules.Reporting.Services;

public class ReportingFavoritesService : IReportingFavoritesService
{
    private readonly ApplicationDbContext _db;

    public ReportingFavoritesService(ApplicationDbContext db)
    {
        _db = db;
    }

    private static string NormalizeScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? "default" : scope!.Trim();

    public async Task<ReportingFavoritesResponse> GetAsync(int userId, string scope, CancellationToken ct = default)
    {
        scope = NormalizeScope(scope);
        var rows = await _db.Set<ReportingFavorite>()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Scope == scope)
            .OrderBy(x => x.Position).ThenBy(x => x.CreatedAt)
            .ToListAsync(ct);

        // Defence-in-depth against the "view-all" tenant filter bypass: when the
        // ambient tenant is -1 the global TenantId query filter is disabled, so
        // a user with the same (Scope, WidgetId) pinned across multiple tenants
        // gets duplicate rows back — which produces duplicate React keys and
        // ghost widgets on the client. Collapse duplicates here by WidgetId,
        // keeping the earliest (Position, CreatedAt) copy.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<ReportingFavoriteDto>(rows.Count);
        foreach (var r in rows)
        {
            if (!seen.Add(r.WidgetId)) continue;
            items.Add(new ReportingFavoriteDto
            {
                WidgetId = r.WidgetId,
                Title = r.Title,
                Source = r.Source,
                Position = r.Position,
            });
        }

        return new ReportingFavoritesResponse
        {
            Scope = scope,
            Items = items,
        };
    }

    public async Task<ReportingFavoriteDto> UpsertAsync(int userId, UpsertReportingFavoriteRequest req, CancellationToken ct = default)
    {
        var scope = NormalizeScope(req.Scope);
        var row = await _db.Set<ReportingFavorite>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Scope == scope && x.WidgetId == req.WidgetId, ct);

        if (row == null)
        {
            row = new ReportingFavorite
            {
                UserId = userId,
                Scope = scope,
                WidgetId = req.WidgetId,
                Title = req.Title,
                Source = req.Source,
                Position = req.Position,
            };
            _db.Set<ReportingFavorite>().Add(row);
        }
        else
        {
            row.Title = req.Title;
            row.Source = req.Source;
            row.Position = req.Position;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return new ReportingFavoriteDto
        {
            WidgetId = row.WidgetId,
            Title = row.Title,
            Source = row.Source,
            Position = row.Position,
        };
    }

    public async Task<bool> DeleteAsync(int userId, string scope, string widgetId, CancellationToken ct = default)
    {
        scope = NormalizeScope(scope);
        var row = await _db.Set<ReportingFavorite>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Scope == scope && x.WidgetId == widgetId, ct);
        if (row == null) return false;
        _db.Set<ReportingFavorite>().Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> DeleteAllAsync(int userId, string scope, CancellationToken ct = default)
    {
        scope = NormalizeScope(scope);
        var rows = await _db.Set<ReportingFavorite>()
            .Where(x => x.UserId == userId && x.Scope == scope)
            .ToListAsync(ct);
        if (rows.Count == 0) return 0;
        _db.Set<ReportingFavorite>().RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task ReorderAsync(int userId, ReorderReportingFavoritesRequest req, CancellationToken ct = default)
    {
        var scope = NormalizeScope(req.Scope);
        var rows = await _db.Set<ReportingFavorite>()
            .Where(x => x.UserId == userId && x.Scope == scope)
            .ToListAsync(ct);

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < req.OrderedWidgetIds.Count; i++)
            index[req.OrderedWidgetIds[i]] = i;

        foreach (var r in rows)
        {
            if (index.TryGetValue(r.WidgetId, out var pos))
            {
                r.Position = pos;
                r.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}