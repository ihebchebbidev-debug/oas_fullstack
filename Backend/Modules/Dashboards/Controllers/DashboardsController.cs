using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Infrastructure;
using MyApi.Modules.Dashboards.DTOs;
using MyApi.Modules.Dashboards.Models;
using MyApi.Modules.Shared.DTOs;

namespace MyApi.Modules.Dashboards.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardsController : ControllerBase
    {
        private readonly ITenantDbContextFactory _dbFactory;
        private readonly ILogger<DashboardsController> _logger;
        private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public DashboardsController(
            ITenantDbContextFactory dbFactory,
            ILogger<DashboardsController> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        private string GetTenant() =>
            Request.Headers.TryGetValue(TenantMiddleware.TenantHeaderName, out var t) ? t.ToString() : "";

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";

        private DashboardDto ToDto(Dashboard d) => new()
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            TemplateKey = d.TemplateKey,
            IsDefault = d.IsDefault,
            IsShared = d.IsShared,
            SharedWithRoles = d.SharedWithRoles != null
                ? JsonSerializer.Deserialize<object>(d.SharedWithRoles, _jsonOpts)
                : null,
            CreatedBy = int.TryParse(d.CreatedBy, out var uid) ? uid : 0,
            Widgets = JsonSerializer.Deserialize<object>(d.Widgets, _jsonOpts) ?? new object[] { },
            GridSettings = d.GridSettings != null
                ? JsonSerializer.Deserialize<object>(d.GridSettings, _jsonOpts)
                : null,
            CreatedAt = d.CreatedAt.ToString("o"),
            UpdatedAt = d.UpdatedAt.ToString("o"),
        };

        // ─── GET /api/Dashboards ──────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            await using var db = _dbFactory.CreateDbContext(GetTenant());
            var list = await db.Dashboards
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .OrderByDescending(d => d.UpdatedAt)
                .ToListAsync();

            return Ok(list.Select(ToDto));
        }

        // ─── GET /api/Dashboards/{id} ─────────────────────────────
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            await using var db = _dbFactory.CreateDbContext(GetTenant());
            var d = await db.Dashboards
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (d == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Dashboard not found"));

            return Ok(ToDto(d));
        }

        // ─── POST /api/Dashboards ─────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDashboardDto dto)
        {
            await using var db = _dbFactory.CreateDbContext(GetTenant());

            var dashboard = new Dashboard
            {
                Name = dto.Name,
                Description = dto.Description,
                TemplateKey = dto.TemplateKey,
                IsShared = dto.IsShared ?? false,
                SharedWithRoles = dto.SharedWithRoles != null
                    ? JsonSerializer.Serialize(dto.SharedWithRoles, _jsonOpts)
                    : null,
                Widgets = JsonSerializer.Serialize(dto.Widgets, _jsonOpts),
                GridSettings = dto.GridSettings != null
                    ? JsonSerializer.Serialize(dto.GridSettings, _jsonOpts)
                    : null,
                CreatedBy = GetUserId(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            db.Dashboards.Add(dashboard);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = dashboard.Id }, ToDto(dashboard));
        }

        // ─── PUT /api/Dashboards/{id} ─────────────────────────────
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDashboardDto dto)
        {
            await using var db = _dbFactory.CreateDbContext(GetTenant());
            var dashboard = await db.Dashboards
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (dashboard == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Dashboard not found"));

            if (dto.Name != null) dashboard.Name = dto.Name;
            if (dto.Description != null) dashboard.Description = dto.Description;
            if (dto.IsShared.HasValue) dashboard.IsShared = dto.IsShared.Value;
            if (dto.SharedWithRoles != null)
                dashboard.SharedWithRoles = JsonSerializer.Serialize(dto.SharedWithRoles, _jsonOpts);
            if (dto.Widgets != null)
                dashboard.Widgets = JsonSerializer.Serialize(dto.Widgets, _jsonOpts);
            if (dto.GridSettings != null)
                dashboard.GridSettings = JsonSerializer.Serialize(dto.GridSettings, _jsonOpts);

            dashboard.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Ok(ToDto(dashboard));
        }

        // ─── DELETE /api/Dashboards/{id} ──────────────────────────
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await using var db = _dbFactory.CreateDbContext(GetTenant());
            var dashboard = await db.Dashboards
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (dashboard == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Dashboard not found"));

            dashboard.IsDeleted = true;
            dashboard.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return NoContent();
        }

        // ─── POST /api/Dashboards/{id}/duplicate ──────────────────
        [HttpPost("{id:int}/duplicate")]
        public async Task<IActionResult> Duplicate(int id, [FromBody] DuplicateDashboardDto dto)
        {
            await using var db = _dbFactory.CreateDbContext(GetTenant());
            var source = await db.Dashboards
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (source == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Dashboard not found"));

            var dup = new Dashboard
            {
                Name = dto.Name,
                Description = source.Description,
                TemplateKey = source.TemplateKey,
                IsShared = false,
                SharedWithRoles = source.SharedWithRoles,
                Widgets = source.Widgets,
                GridSettings = source.GridSettings,
                CreatedBy = GetUserId(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            db.Dashboards.Add(dup);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = dup.Id }, ToDto(dup));
        }

        // ─── POST /api/Dashboards/{id}/share ───────────────────────
        [HttpPost("{id:int}/share")]
        public async Task<IActionResult> GenerateShareLink(int id, [FromBody] ShareDashboardRequest? request)
        {
            await using var db = _dbFactory.CreateDbContext(GetTenant());
            var dashboard = await db.Dashboards
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (dashboard == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Dashboard not found"));

            if (string.IsNullOrEmpty(dashboard.ShareToken))
            {
                dashboard.ShareToken = Guid.NewGuid().ToString("N")[..16];
                dashboard.IsPublic = true;
                dashboard.SharedAt = DateTime.UtcNow;
            }

            // Store data snapshot if provided
            if (request?.DataSnapshot != null)
            {
                dashboard.SnapshotData = JsonSerializer.Serialize(request.DataSnapshot, _jsonOpts);
                dashboard.SnapshotAt = DateTime.UtcNow;
            }

            dashboard.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = new SharedDashboardInfoDto
            {
                ShareToken = dashboard.ShareToken,
                PublicUrl = $"{baseUrl}/public/dashboards/{dashboard.ShareToken}",
                IsPublic = dashboard.IsPublic,
            };

            return Ok(ApiResponse<SharedDashboardInfoDto>.SuccessResponse(result, "Share link generated"));
        }

        // ─── DELETE /api/Dashboards/{id}/share ─────────────────────
        [HttpDelete("{id:int}/share")]
        public async Task<IActionResult> RevokeShareLink(int id)
        {
            await using var db = _dbFactory.CreateDbContext(GetTenant());
            var dashboard = await db.Dashboards
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (dashboard == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Dashboard not found"));

            dashboard.ShareToken = null;
            dashboard.IsPublic = false;
            dashboard.SharedAt = null;
            dashboard.SnapshotData = null;
            dashboard.SnapshotAt = null;
            dashboard.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Ok(ApiResponse<object>.SuccessResponse((object?)null, "Share link revoked"));
        }

        // ─── GET /api/Dashboards/public/{token} ───────────────────
        // NOTE: This endpoint uses the DEFAULT database (no tenant header from
        // anonymous visitors). If multi-tenant sharing is needed, encode tenant
        // in the share token or URL path.
        [AllowAnonymous]
        [HttpGet("public/{token}")]
        public async Task<IActionResult> GetByShareToken(string token)
        {
            // Anonymous callers won't send X-Tenant — use default connection
            var tenant = Request.Headers.TryGetValue(TenantMiddleware.TenantHeaderName, out var t) ? t.ToString() : "";
            await using var db = _dbFactory.CreateDbContext(tenant);

            var dashboard = await db.Dashboards
                .AsNoTracking()
                .FirstOrDefaultAsync(d =>
                    d.ShareToken == token &&
                    d.IsPublic &&
                    !d.IsDeleted);

            if (dashboard == null)
                return NotFound(new { message = "Dashboard not found or link expired" });

            var result = new PublicDashboardDto
            {
                Id = dashboard.Id,
                Name = dashboard.Name,
                Description = dashboard.Description,
                Widgets = JsonSerializer.Deserialize<object>(dashboard.Widgets, _jsonOpts),
                GridSettings = dashboard.GridSettings != null
                    ? JsonSerializer.Deserialize<object>(dashboard.GridSettings, _jsonOpts)
                    : null,
                DataSnapshot = dashboard.SnapshotData != null
                    ? JsonSerializer.Deserialize<object>(dashboard.SnapshotData, _jsonOpts)
                    : null,
                SnapshotAt = dashboard.SnapshotAt,
            };

            return Ok(result);
        }
    }
}
