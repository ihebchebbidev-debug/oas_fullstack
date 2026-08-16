using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common.Models;

namespace MyApi.Modules.OAS.Common.Services;

public interface IOasPluginActivationService
{
    Task<IReadOnlyList<OasPluginActivation>> GetAllAsync(int tenantId);
    Task<bool> IsEnabledAsync(int tenantId, string pluginCode);
    Task<(bool success, string? error, IReadOnlyList<string> cascaded)> SetEnabledAsync(int tenantId, string pluginCode, bool enabled, string? actor);
    Task<IReadOnlyList<string>> BulkResetAsync(int tenantId, string? actor);
}

/// <summary>
/// Reproduces `activationStore.ts:59-88` exactly (spec §5.2 table 16, §1.5):
/// isCore plugins can never be disabled; disabling any other plugin cascades
/// to every plugin that (directly or transitively) depends on it. Every write
/// here lands in oas_audit_log automatically — `oas_plugin_activations` is in
/// the generic `trg_oas_audit_*` trigger's table list (003_triggers.sql),
/// same as every other OAS table (spec decision v12), no per-action call needed.
/// </summary>
public class OasPluginActivationService : IOasPluginActivationService
{
    private readonly OasDbContext _db;

    public OasPluginActivationService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasPluginActivation>> GetAllAsync(int tenantId)
    {
        var existing = await _db.PluginActivations.ToListAsync();
        var existingCodes = existing.Select(e => e.PluginCode).ToHashSet();

        // Any plugin never explicitly toggled defaults to enabled (matches
        // `activationStore.ts` treating "absent from storage" as enabled).
        var missing = OasPluginRegistry.All
            .Where(p => !existingCodes.Contains(p.Code))
            .Select(p => new OasPluginActivation { TenantId = tenantId, PluginCode = p.Code, Enabled = true });

        return existing.Concat(missing).OrderBy(e => e.PluginCode).ToList();
    }

    public async Task<bool> IsEnabledAsync(int tenantId, string pluginCode)
    {
        if (OasPluginRegistry.IsCore(pluginCode)) return true;
        var row = await _db.PluginActivations.FirstOrDefaultAsync(x => x.PluginCode == pluginCode);
        return row?.Enabled ?? true;
    }

    public async Task<(bool success, string? error, IReadOnlyList<string> cascaded)> SetEnabledAsync(int tenantId, string pluginCode, bool enabled, string? actor)
    {
        if (!OasPluginRegistry.IsKnown(pluginCode))
        {
            return (false, "unknown_plugin", Array.Empty<string>());
        }

        if (!enabled && OasPluginRegistry.IsCore(pluginCode))
        {
            return (false, "core_plugin_cannot_be_disabled", Array.Empty<string>());
        }

        var cascaded = new List<string>();
        await UpsertAsync(tenantId, pluginCode, enabled, actor);

        if (!enabled)
        {
            // Disabling cascades to every transitive dependent — never to
            // dependencies, and never re-enables anything on the reverse
            // path (matches activationStore.ts: enabling a plugin does NOT
            // auto-enable its dependents).
            foreach (var dependent in OasPluginRegistry.TransitiveDependents(pluginCode))
            {
                if (OasPluginRegistry.IsCore(dependent)) continue; // defensive; cores never depend on non-cores in the registry
                await UpsertAsync(tenantId, dependent, false, actor);
                cascaded.Add(dependent);
            }
        }

        return (true, null, cascaded);
    }

    public async Task<IReadOnlyList<string>> BulkResetAsync(int tenantId, string? actor)
    {
        var reset = new List<string>();
        foreach (var plugin in OasPluginRegistry.All)
        {
            await UpsertAsync(tenantId, plugin.Code, true, actor);
            reset.Add(plugin.Code);
        }
        return reset;
    }

    private async Task UpsertAsync(int tenantId, string pluginCode, bool enabled, string? actor)
    {
        var row = await _db.PluginActivations.FirstOrDefaultAsync(x => x.PluginCode == pluginCode);
        if (row is null)
        {
            _db.PluginActivations.Add(new OasPluginActivation
            {
                TenantId = tenantId,
                PluginCode = pluginCode,
                Enabled = enabled,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = actor,
            });
        }
        else
        {
            row.Enabled = enabled;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            row.UpdatedBy = actor;
        }
        await _db.SaveChangesAsync();
    }
}
