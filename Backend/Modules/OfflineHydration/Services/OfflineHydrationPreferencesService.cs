using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.OfflineHydration.DTOs;
using MyApi.Modules.OfflineHydration.Models;

namespace MyApi.Modules.OfflineHydration.Services;

public class OfflineHydrationPreferencesService : IOfflineHydrationPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly ApplicationDbContext _context;

    public OfflineHydrationPreferencesService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OfflineHydrationModulesDto> GetForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var row = await _context.Set<OfflineHydrationPreference>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (row == null)
        {
            return new OfflineHydrationModulesDto
            {
                Modules = new Dictionary<string, bool>(),
                UpdatedAt = null,
            };
        }

        return new OfflineHydrationModulesDto
        {
            Modules = ParseModules(row.ModulesJson),
            UpdatedAt = row.UpdatedAt,
        };
    }

    public async Task<OfflineHydrationModulesDto> UpsertForUserAsync(int userId, Dictionary<string, bool> modules, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeModules(modules);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);

        var row = await _context.Set<OfflineHydrationPreference>()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (row == null)
        {
            row = new OfflineHydrationPreference
            {
                UserId = userId,
                ModulesJson = json,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.Set<OfflineHydrationPreference>().Add(row);
        }
        else
        {
            row.ModulesJson = json;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new OfflineHydrationModulesDto
        {
            Modules = ParseModules(row.ModulesJson),
            UpdatedAt = row.UpdatedAt,
        };
    }

    private static Dictionary<string, bool> ParseModules(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return new Dictionary<string, bool>();

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(json, JsonOptions);
            return dict ?? new Dictionary<string, bool>();
        }
        catch
        {
            return new Dictionary<string, bool>();
        }
    }

    /// <summary>
    /// Keep only explicit false entries (matches frontend semantics: missing key = enabled).
    /// </summary>
    private static Dictionary<string, bool> NormalizeModules(Dictionary<string, bool> modules)
    {
        var outDict = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var kv in modules)
        {
            if (kv.Value == false)
                outDict[kv.Key] = false;
        }
        return outDict;
    }
}
