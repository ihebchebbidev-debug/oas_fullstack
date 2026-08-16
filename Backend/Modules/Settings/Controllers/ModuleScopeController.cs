using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Settings.DTOs;
using MyApi.Modules.Settings.Models;
using MyApi.Modules.Settings.Services;

namespace MyApi.Modules.Settings.Controllers
{
    /// <summary>
    /// Manage per-module data scope (shared vs per_company).
    /// Only MainAdminUser can read/write — module scope changes how every
    /// query in the system filters by TenantId.
    /// </summary>
    [ApiController]
    [Route("api/module-scope")]
    [Authorize]
    public class ModuleScopeController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IModuleScopeProvider _provider;
        private readonly ILogger<ModuleScopeController> _logger;

        public ModuleScopeController(
            ApplicationDbContext db,
            IModuleScopeProvider provider,
            ILogger<ModuleScopeController> logger)
        {
            _db = db;
            _provider = provider;
            _logger = logger;
        }

        private bool IsMainAdmin()
        {
            var loginType = User.FindFirst("login_type")?.Value
                            ?? User.FindFirst("loginType")?.Value;
            return string.Equals(loginType, "admin", StringComparison.OrdinalIgnoreCase);
        }

        private int? GetUserId()
        {
            var raw = User.FindFirst("user_id")?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var id) ? id : (int?)null;
        }

        [HttpGet]
        public async Task<ActionResult<List<ModuleScopeDto>>> List()
        {
            var rows = await _db.Set<ModuleScopeSetting>()
                .AsNoTracking()
                .OrderBy(m => m.ModuleKey)
                .ToListAsync();

            return Ok(rows.Select(r => new ModuleScopeDto
            {
                ModuleKey = r.ModuleKey,
                Scope = r.Scope,
                UpdatedAt = r.UpdatedAt,
            }).ToList());
        }

        [HttpGet("{moduleKey}")]
        public async Task<ActionResult<ModuleScopeDto>> Get(string moduleKey)
        {
            var key = (moduleKey ?? string.Empty).Trim().ToLowerInvariant();
            var row = await _db.Set<ModuleScopeSetting>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ModuleKey == key);

            if (row == null)
            {
                // Treat missing rows as the default scope.
                return Ok(new ModuleScopeDto
                {
                    ModuleKey = key,
                    Scope = "per_company",
                    UpdatedAt = DateTime.UtcNow,
                });
            }

            return Ok(new ModuleScopeDto
            {
                ModuleKey = row.ModuleKey,
                Scope = row.Scope,
                UpdatedAt = row.UpdatedAt,
            });
        }

        [HttpPut("{moduleKey}")]
        public async Task<ActionResult<ModuleScopeDto>> Update(
            string moduleKey,
            [FromBody] UpdateModuleScopeRequest body)
        {
            if (!IsMainAdmin())
                return Forbid();

            var key = (moduleKey ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "moduleKey is required" });

            var newScope = (body?.Scope ?? "per_company").Trim().ToLowerInvariant();
            if (newScope != "shared" && newScope != "per_company")
                return BadRequest(new { error = "Scope must be 'shared' or 'per_company'." });

            var row = await _db.Set<ModuleScopeSetting>()
                .FirstOrDefaultAsync(m => m.ModuleKey == key);

            if (row == null)
            {
                row = new ModuleScopeSetting { ModuleKey = key, Scope = newScope };
                _db.Add(row);
            }
            else
            {
                row.Scope = newScope;
            }
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedByUserId = GetUserId();

            await _db.SaveChangesAsync();
            _provider.Invalidate();

            _logger.LogInformation(
                "ModuleScope updated: {Module} → {Scope} by user {UserId}",
                key, newScope, row.UpdatedByUserId);

            return Ok(new ModuleScopeDto
            {
                ModuleKey = row.ModuleKey,
                Scope = row.Scope,
                UpdatedAt = row.UpdatedAt,
            });
        }

        /// <summary>
        /// Bulk upsert. Body: [{ moduleKey, scope }, …]. Atomic — either all rows save or none.
        /// Used by the Module Data Scope dialog's single "Save" button.
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<List<ModuleScopeDto>>> BulkUpdate(
            [FromBody] List<BulkModuleScopeItem> body)
        {
            if (!IsMainAdmin())
                return Forbid();
            if (body == null || body.Count == 0)
                return BadRequest(new { error = "Body must contain at least one item." });

            var userId = GetUserId();
            var now = DateTime.UtcNow;
            var saved = new List<ModuleScopeDto>(body.Count);

            foreach (var item in body)
            {
                var key = (item?.ModuleKey ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(key)) continue;

                var scope = (item!.Scope ?? "per_company").Trim().ToLowerInvariant();
                if (scope != "shared" && scope != "per_company")
                    return BadRequest(new { error = $"Scope for '{key}' must be 'shared' or 'per_company'." });

                var row = await _db.Set<ModuleScopeSetting>()
                    .FirstOrDefaultAsync(m => m.ModuleKey == key);
                if (row == null)
                {
                    row = new ModuleScopeSetting { ModuleKey = key, Scope = scope };
                    _db.Add(row);
                }
                else
                {
                    row.Scope = scope;
                }
                row.UpdatedAt = now;
                row.UpdatedByUserId = userId;

                saved.Add(new ModuleScopeDto
                {
                    ModuleKey = row.ModuleKey,
                    Scope = row.Scope,
                    UpdatedAt = row.UpdatedAt,
                });
            }

            await _db.SaveChangesAsync();
            _provider.Invalidate();

            _logger.LogInformation(
                "ModuleScope BULK updated ({Count} rows) by user {UserId}",
                saved.Count, userId);

            return Ok(saved);
        }
    }
}

