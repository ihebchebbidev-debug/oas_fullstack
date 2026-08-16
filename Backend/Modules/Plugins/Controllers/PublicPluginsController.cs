using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Infrastructure;
using MyApi.Modules.Plugins.DTOs;
using MyApi.Modules.Plugins.Services;

namespace MyApi.Modules.Plugins.Controllers
{
    /// <summary>
    /// FULLY OPEN cross-tenant module (plugin) management API.
    ///
    /// No auth, no API key, no X-Tenant header — the tenant is always passed
    /// explicitly as `?tenant=slug`. Designed for an external admin app:
    /// in-app users can only READ their modules (they request changes by email),
    /// while this API is the only write path for activation.
    ///
    /// Every write applies the SAME dependency rules as the in-app resolver:
    ///   enable  → whole transitive dependency chain is enabled too
    ///   disable → 409 unless cascade=true, then all transitive dependents off
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("api/public/plugins")]
    public class PublicPluginsController : ControllerBase
    {
        private readonly ITenantDbContextFactory _dbFactory;
        private readonly ILogger<PublicPluginsController> _logger;

        public PublicPluginsController(
            ITenantDbContextFactory dbFactory,
            ILogger<PublicPluginsController> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        // ── helpers ───────────────────────────────────────────────

        private static List<string> AllTenants()
        {
            var list = TenantConnectionResolver.GetConfiguredTenantConnections()
                .Select(t => t.Tenant)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!list.Any(t => string.Equals(t, "default", StringComparison.OrdinalIgnoreCase)))
                list.Insert(0, "default");
            return list;
        }

        private ApplicationDbContext Db(string? tenant) => _dbFactory.CreateDbContext(tenant);

        /// <summary>Effective on/off for every known plugin (mirrors the frontend resolver).</summary>
        private static Dictionary<string, bool> Resolve(Dictionary<string, bool> stored)
        {
            var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in KnownPlugins.All)
            {
                if (e.IsCore) { map[e.Code] = true; continue; }
                var on = !stored.TryGetValue(e.Code, out var v) || v;
                if (on)
                {
                    on = KnownPlugins.TransitiveDependencies(e.Code)
                        .All(dep => KnownPlugins.IsCore(dep) || !stored.TryGetValue(dep, out var dv) || dv);
                }
                map[e.Code] = on;
            }
            return map;
        }

        private static object Snapshot(string tenant, List<PluginActivationDto> rows)
        {
            var stored = rows
                .GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().IsEnabled, StringComparer.OrdinalIgnoreCase);
            var effective = Resolve(stored);
            return new
            {
                tenant,
                total = KnownPlugins.All.Count,
                active = effective.Count(kv => kv.Value),
                modules = KnownPlugins.All.Select(e => new
                {
                    code = e.Code,
                    isCore = e.IsCore,
                    dependencies = e.Dependencies,
                    stored = stored.TryGetValue(e.Code, out var s) ? (bool?)s : null,
                    isEnabled = effective[e.Code],
                    disabledByDependency = effective[e.Code] == false &&
                        (!stored.TryGetValue(e.Code, out var sv) || sv),
                }),
                activations = rows,
            };
        }

        // ── discovery ─────────────────────────────────────────────

        /// <summary>All tenants this API can manage.</summary>
        [HttpGet("tenants")]
        public IActionResult Tenants() =>
            Ok(new { success = true, data = AllTenants().Select(t => new { tenant = t }) });

        /// <summary>Static dependency graph — copy the cascade rules into any client.</summary>
        [HttpGet("graph")]
        public IActionResult Graph() => Ok(new
        {
            success = true,
            data = KnownPlugins.All.Select(e => new
            {
                code = e.Code,
                isCore = e.IsCore,
                dependencies = e.Dependencies,
                transitiveDependencies = KnownPlugins.TransitiveDependencies(e.Code),
                transitiveDependents = KnownPlugins.TransitiveDependents(e.Code),
            }),
        });

        // ── read ──────────────────────────────────────────────────

        /// <summary>One tenant's module state (stored + effective).</summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string tenant = "default")
        {
            await using var db = Db(tenant);
            var rows = await new PluginService(db).GetActivationsAsync();
            return Ok(new { success = true, data = Snapshot(tenant, rows) });
        }

        /// <summary>Every tenant in one call. `?tenant=` narrows it to one.</summary>
        [HttpGet("all")]
        public async Task<IActionResult> All([FromQuery] string? tenant)
        {
            var tenants = string.IsNullOrWhiteSpace(tenant) ? AllTenants() : new List<string> { tenant.Trim().ToLowerInvariant() };
            var results = new List<object>();
            var errors = new List<object>();
            foreach (var t in tenants)
            {
                try
                {
                    await using var db = Db(t);
                    var rows = await new PluginService(db).GetActivationsAsync();
                    results.Add(Snapshot(t, rows));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PUBLIC-PLUGINS: tenant '{Tenant}' unreachable", t);
                    errors.Add(new { tenant = t, error = ex.Message });
                }
            }
            return Ok(new { success = true, data = results, errors });
        }

        /// <summary>Dry-run: what a toggle would change, without writing.</summary>
        [HttpGet("preview/{code}")]
        public async Task<IActionResult> Preview(string code, [FromQuery] bool isEnabled, [FromQuery] string tenant = "default")
        {
            if (!KnownPlugins.Exists(code))
                return NotFound(new { success = false, error = "unknown", message = $"Unknown plugin code '{code}'." });

            await using var db = Db(tenant);
            var rows = await new PluginService(db).GetActivationsAsync();
            var stored = rows.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
                             .ToDictionary(g => g.Key, g => g.First().IsEnabled, StringComparer.OrdinalIgnoreCase);

            if (!isEnabled && KnownPlugins.IsCore(code))
                return BadRequest(new { success = false, error = "coreLocked", message = $"Plugin '{code}' is core and cannot be disabled.", code });

            var alsoEnabled = isEnabled
                ? KnownPlugins.TransitiveDependencies(code)
                    .Where(c => stored.TryGetValue(c, out var v) && !v).ToList()
                : new List<string>();
            var alsoDisabled = !isEnabled
                ? KnownPlugins.TransitiveDependents(code)
                    .Where(c => !KnownPlugins.IsCore(c))
                    .Where(c => !stored.TryGetValue(c, out var v) || v).ToList()
                : new List<string>();

            return Ok(new { success = true, data = new { tenant, code, isEnabled, alsoEnabled, alsoDisabled } });
        }

        // ── write ─────────────────────────────────────────────────

        /// <summary>Toggle one module for one tenant.</summary>
        [HttpPatch("{code}")]
        public async Task<IActionResult> Toggle(string code, [FromBody] PluginToggleRequest body, [FromQuery] string tenant = "default")
        {
            try
            {
                await using var db = Db(tenant);
                var svc = new PluginService(db);
                var dto = await svc.SetActivationAsync(code, body.IsEnabled, null, body.Cascade);
                var rows = await svc.GetActivationsAsync();
                return Ok(new { success = true, data = dto, snapshot = Snapshot(tenant, rows) });
            }
            catch (PluginCoreLockedException ex)
            {
                return BadRequest(new { success = false, error = "coreLocked", message = ex.Message, code = ex.Code });
            }
            catch (PluginDependencyConflictException ex)
            {
                return Conflict(new
                {
                    success = false,
                    error = "dependencyConflict",
                    message = ex.Message,
                    code = ex.Code,
                    blockingDependents = ex.BlockingDependents,
                });
            }
            catch (PluginUnknownException ex)
            {
                return NotFound(new { success = false, error = "unknown", message = ex.Message });
            }
        }

        /// <summary>Toggle many modules for one tenant.</summary>
        [HttpPost("bulk")]
        public async Task<IActionResult> Bulk([FromBody] PluginBulkToggleRequest body, [FromQuery] string tenant = "default")
        {
            await using var db = Db(tenant);
            var svc = new PluginService(db);
            var data = await svc.BulkSetAsync(body.Codes ?? new List<string>(), body.IsEnabled, null, body.Cascade);
            var rows = await svc.GetActivationsAsync();
            return Ok(new { success = true, data, snapshot = Snapshot(tenant, rows) });
        }

        public class BroadcastRequest
        {
            public string Code { get; set; } = "";
            public bool IsEnabled { get; set; }
            public bool Cascade { get; set; } = true;
            /// <summary>Omit / empty = every configured tenant.</summary>
            public List<string>? Tenants { get; set; }
        }

        /// <summary>Apply the same module change to many tenants at once.</summary>
        [HttpPost("broadcast")]
        public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Code))
                return BadRequest(new { success = false, error = "invalidRequest", message = "code is required." });
            if (!KnownPlugins.Exists(body.Code))
                return NotFound(new { success = false, error = "unknown", message = $"Unknown plugin code '{body.Code}'." });
            if (!body.IsEnabled && KnownPlugins.IsCore(body.Code))
                return BadRequest(new { success = false, error = "coreLocked", message = $"Plugin '{body.Code}' is core and cannot be disabled.", code = body.Code });

            var tenants = body.Tenants != null && body.Tenants.Count > 0
                ? body.Tenants.Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList()
                : AllTenants();

            var applied = new List<object>();
            var failed = new List<object>();
            foreach (var t in tenants)
            {
                try
                {
                    await using var db = Db(t);
                    var svc = new PluginService(db);
                    var dto = await svc.SetActivationAsync(body.Code, body.IsEnabled, null, body.Cascade);
                    applied.Add(new { tenant = t, code = dto.Code, isEnabled = dto.IsEnabled, updatedAt = dto.UpdatedAt });
                }
                catch (PluginDependencyConflictException ex)
                {
                    failed.Add(new { tenant = t, error = "dependencyConflict", blockingDependents = ex.BlockingDependents });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PUBLIC-PLUGINS: broadcast failed for '{Tenant}'", t);
                    failed.Add(new { tenant = t, error = ex.Message });
                }
            }
            return Ok(new { success = failed.Count == 0, data = applied, errors = failed });
        }
    }
}
