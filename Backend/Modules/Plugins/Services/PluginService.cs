using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Plugins.DTOs;
using MyApi.Modules.Plugins.Models;

namespace MyApi.Modules.Plugins.Services
{
    public class PluginService : IPluginService
    {
        private readonly ApplicationDbContext _db;

        public PluginService(ApplicationDbContext db) { _db = db; }

        public async Task<List<PluginActivationDto>> GetActivationsAsync()
        {
            // Simple, complete snapshot: every known plugin with its EFFECTIVE
            // state (explicit row + transitive dependency chain + core lock).
            // The backend enforces nothing at API level — it only reports what
            // is activated/deactivated; the frontend does the gating.
            var rows = await _db.ActivatedModules.AsNoTracking().ToListAsync();
            var stored = rows
                .GroupBy(r => r.PluginCode)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var result = new List<PluginActivationDto>();

            foreach (var entry in KnownPlugins.All)
            {
                stored.TryGetValue(entry.Code, out var row);

                bool enabled;
                if (entry.IsCore)
                {
                    enabled = true;
                }
                else if (row != null && !row.IsEnabled)
                {
                    enabled = false;
                }
                else
                {
                    enabled = KnownPlugins.TransitiveDependencies(entry.Code)
                        .All(dep => !stored.TryGetValue(dep, out var d) || d.IsEnabled);
                }

                result.Add(new PluginActivationDto
                {
                    Code = entry.Code,
                    IsEnabled = enabled,
                    UpdatedAt = row?.UpdatedAt ?? default,
                });
            }

            // Unknown codes stored by a newer frontend still surface as-is.
            foreach (var row in rows.Where(r => !KnownPlugins.ByCode.ContainsKey(r.PluginCode)))
            {
                result.Add(new PluginActivationDto
                {
                    Code = row.PluginCode,
                    IsEnabled = row.IsEnabled,
                    UpdatedAt = row.UpdatedAt,
                });
            }

            return result;
        }


        public Task<PluginActivationDto> SetActivationAsync(string code, bool isEnabled, int? userId)
            => SetActivationAsync(code, isEnabled, userId, cascade: false);

        public async Task<PluginActivationDto> SetActivationAsync(string code, bool isEnabled, int? userId, bool cascade)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new PluginUnknownException(code ?? "");

            // Validate against known catalog if registered. Allow unknown codes
            // through (frontend may add new manifests ahead of backend deploys),
            // but still apply core/dependency checks when the code IS known.
            var known = KnownPlugins.ByCode.TryGetValue(code, out var entry) ? entry : null;

            if (!isEnabled && known != null && known.IsCore)
                throw new PluginCoreLockedException(code);

            // ── Codes that must be written alongside the requested one ──
            var alsoWrite = new List<string>();

            if (known != null && isEnabled)
            {
                // Enabling auto-enables the whole transitive dependency chain,
                // otherwise the plugin would resolve to "off" anyway.
                alsoWrite.AddRange(KnownPlugins.TransitiveDependencies(code));
            }
            else if (known != null && !isEnabled)
            {
                // Find dependents (transitive) that are still enabled
                // (no row OR row.IsEnabled = true).
                var dependents = KnownPlugins.TransitiveDependents(code)
                    .Where(c => !KnownPlugins.IsCore(c))
                    .ToList();
                if (dependents.Any())
                {
                    var rowMap = (await _db.ActivatedModules
                        .Where(a => dependents.Contains(a.PluginCode))
                        .ToListAsync())
                        .ToDictionary(a => a.PluginCode);
                    var blocking = dependents
                        .Where(dc =>
                        {
                            rowMap.TryGetValue(dc, out var r);
                            return r == null || r.IsEnabled; // default-on or explicitly on
                        })
                        .ToList();
                    if (blocking.Any())
                    {
                        if (!cascade)
                            throw new PluginDependencyConflictException(code, blocking);
                        alsoWrite.AddRange(blocking);
                    }
                }
            }

            var targets = new List<string> { code };
            targets.AddRange(alsoWrite.Where(c => c != code));
            targets = targets.Distinct().ToList();

            var rows = await _db.ActivatedModules
                .Where(a => targets.Contains(a.PluginCode))
                .ToListAsync();

            ActivatedModule? primary = null;
            foreach (var target in targets)
            {
                var row = rows.FirstOrDefault(r => r.PluginCode == target);
                if (row == null)
                {
                    row = new ActivatedModule
                    {
                        PluginCode = target,
                        IsEnabled = isEnabled,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = userId,
                    };
                    _db.ActivatedModules.Add(row);
                }
                else
                {
                    row.IsEnabled = isEnabled;
                    row.UpdatedAt = DateTime.UtcNow;
                    row.UpdatedBy = userId;
                }
                if (target == code) primary = row;
            }

            await _db.SaveChangesAsync();
            var existing = primary!;

            return new PluginActivationDto
            {
                Code = existing.PluginCode,
                IsEnabled = existing.IsEnabled,
                UpdatedAt = existing.UpdatedAt,
            };
        }

        public Task<List<PluginActivationDto>> BulkSetAsync(List<string> codes, bool isEnabled, int? userId)
            => BulkSetAsync(codes, isEnabled, userId, cascade: false);

        public async Task<List<PluginActivationDto>> BulkSetAsync(List<string> codes, bool isEnabled, int? userId, bool cascade)
        {
            var results = new List<PluginActivationDto>();
            foreach (var code in codes.Distinct())
            {
                try
                {
                    var dto = await SetActivationAsync(code, isEnabled, userId, cascade);
                    results.Add(dto);
                }
                catch (PluginCoreLockedException) { /* skip core */ }
                catch (PluginDependencyConflictException) { /* skip blocked */ }
            }
            return results;
        }

        public async Task<PluginStatsDto> GetStatsAsync()
        {
            var total = KnownPlugins.All.Count;

            var stored = (await _db.ActivatedModules.AsNoTracking().ToListAsync())
                .GroupBy(a => a.PluginCode)
                .ToDictionary(g => g.Key, g => g.First().IsEnabled);

            // Effective state: off when explicitly off OR when any transitive
            // dependency is off. Mirrors the frontend resolver exactly.
            var active = KnownPlugins.All.Count(e =>
            {
                if (e.IsCore) return true;
                if (stored.TryGetValue(e.Code, out var v) && !v) return false;
                return KnownPlugins.TransitiveDependencies(e.Code)
                    .All(dep => !stored.TryGetValue(dep, out var dv) || dv);
            });

            return new PluginStatsDto { Active = active, Total = total };
        }
    }
}
