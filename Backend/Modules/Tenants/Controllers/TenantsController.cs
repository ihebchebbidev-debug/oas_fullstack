using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Infrastructure;
using MyApi.Modules.Roles.Services;
using MyApi.Modules.Tenants.Models;
using MyApi.Modules.Tenants.Services;

namespace MyApi.Modules.Tenants.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TenantsController> _logger;
        private readonly TenantSeeder _tenantSeeder;
        private readonly IPermissionService _permissionService;

        public TenantsController(
            ApplicationDbContext context,
            ILogger<TenantsController> logger,
            TenantSeeder tenantSeeder,
            IPermissionService permissionService)
        {
            _context = context;
            _logger = logger;
            _tenantSeeder = tenantSeeder;
            _permissionService = permissionService;
        }


        /// <summary>
        /// Get the MainAdminUser ID from JWT claims.
        /// Only MainAdminUser (login_type=admin) can manage tenants.
        /// </summary>
        private int? GetMainAdminUserId()
        {
            var loginType = User.FindFirst("login_type")?.Value;
            if (loginType != "admin") return null;

            var userIdClaim = User.FindFirst("UserId")?.Value 
                           ?? User.FindFirst("user_id")?.Value 
                           ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value;
                           
            if (int.TryParse(userIdClaim, out var userId))
                return userId;

            return null;
        }

        /// <summary>
        /// Returns the authenticated regular user's id (login_type=user), if any.
        /// Regular users are created inside ONE company (Users.TenantId) and may
        /// only see that single company in the picker — never switch.
        /// </summary>
        private int? GetRegularUserId()
        {
            var loginType = User.FindFirst("login_type")?.Value;
            if (loginType != "user") return null;

            var userIdClaim = User.FindFirst("UserId")?.Value
                           ?? User.FindFirst("user_id")?.Value
                           ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;

            return int.TryParse(userIdClaim, out var userId) ? userId : (int?)null;
        }

        /// <summary>
        /// GET /api/Tenants — List companies the caller can access.
        ///  - MainAdminUser: every tenant they own in this database.
        ///  - RegularUser without 'settings.switch_company' permission:
        ///                   ONLY the tenant their account belongs to
        ///                   (Users.TenantId → Tenant row in current DB).
        ///                   For TenantId=0 we resolve to the default tenant.
        ///  - RegularUser WITH 'settings.switch_company' permission:
        ///                   every active tenant in the current DB (so the
        ///                   header switcher lets them pick another company).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var adminId = GetMainAdminUserId();
            if (adminId != null)
            {
                var tenants = await _context.Tenants
                    .Where(t => t.MainAdminUserId == adminId.Value)
                    .OrderByDescending(t => t.IsDefault)
                    .ThenBy(t => t.CompanyName)
                    .ToListAsync();

                return Ok(tenants);
            }

            var regularUserId = GetRegularUserId();
            if (regularUserId != null)
            {
                // IgnoreQueryFilters: middleware sets TenantId=0 on this bootstrap
                // call (no X-Target-Tenant yet), which would otherwise filter the
                // user out if they belong to a non-default company.
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.Id == regularUserId.Value && u.IsActive && !u.IsDeleted)
                    .Select(u => new { u.TenantId })
                    .FirstOrDefaultAsync();

                if (user == null) return Ok(Array.Empty<object>());

                // Default behaviour: a regular user is bound to ONE company
                // and cannot switch — unless their role explicitly grants
                // settings.switch_company.
                var canSwitch = await _permissionService.UserHasPermissionAsync(
                    regularUserId.Value, "settings", "switch_company");

                if (canSwitch)
                {
                    var allTenants = await _context.Tenants
                        .Where(t => t.IsActive)
                        .OrderByDescending(t => t.IsDefault)
                        .ThenBy(t => t.CompanyName)
                        .ToListAsync();
                    return Ok(allTenants);
                }

                // TenantId=0 in data rows → the default tenant of this database.
                var match = user.TenantId == 0
                    ? await _context.Tenants.FirstOrDefaultAsync(t => t.IsActive && t.IsDefault)
                    : await _context.Tenants.FirstOrDefaultAsync(t => t.IsActive && t.Id == user.TenantId);

                return Ok(match == null ? Array.Empty<object>() : new[] { match });
            }

            return Forbid();
        }



        /// <summary>
        /// GET /api/Tenants/{id} — Get a single tenant by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var adminId = GetMainAdminUserId();
            if (adminId == null) return Forbid();

            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && t.MainAdminUserId == adminId.Value);

            if (tenant == null) return NotFound();
            return Ok(tenant);
        }

        /// <summary>
        /// POST /api/Tenants — Create a new tenant/company.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTenantRequest request)
        {
            var adminId = GetMainAdminUserId();
            if (adminId == null) return Forbid();

            // Validate required fields + slug format before touching the DB.
            var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(request.CompanyName))
                return BadRequest(new { message = "Company name is required." });
            if (slug.Length < 2 || slug.Length > 50 ||
                !System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$"))
                return BadRequest(new { message = "Slug must be 2-50 characters, lowercase letters, digits or hyphens." });

            // Validate slug uniqueness
            var slugExists = await _context.Tenants.AnyAsync(t => t.Slug == slug);
            if (slugExists)
                return BadRequest(new { message = "A company with this slug already exists." });

            var tenant = new Tenant
            {
                MainAdminUserId = adminId.Value,
                Slug = slug,
                CompanyName = request.CompanyName,
                CompanyLogoUrl = request.CompanyLogoUrl,
                CompanyWebsite = request.CompanyWebsite,
                CompanyPhone = request.CompanyPhone,
                CompanyAddress = request.CompanyAddress,
                CompanyCountry = request.CompanyCountry,
                Industry = request.Industry,
                CompanyEmail = request.CompanyEmail,
                CompanyTagline = request.CompanyTagline,
                CompanyCity = request.CompanyCity,
                CompanyPostalCode = request.CompanyPostalCode,
                CompanyState = request.CompanyState,
                TaxId = request.TaxId,
                RegistrationNumber = request.RegistrationNumber,
                ShareCapital = request.ShareCapital,
                BankName = request.BankName,
                BankAccount = request.BankAccount,
                BankSwift = request.BankSwift,
                ReportFooterMessage = request.ReportFooterMessage,
                IsActive = true,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            // Refresh cache
            TenantSlugCache.Refresh(_context);

            // Seed default data (LookupItems, Currencies, NumberingSettings) from first tenant
            try
            {
                await _tenantSeeder.SeedForNewTenantAsync(tenant.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Tenant seeding failed for {Slug} (Id={Id}), tenant created but without default data",
                    tenant.Slug, tenant.Id);
            }

            _logger.LogInformation("🏢 Tenant created: {Slug} (Id={Id}) by admin {AdminId}",
                tenant.Slug, tenant.Id, adminId.Value);

            return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
        }

        /// <summary>
        /// PUT /api/Tenants/{id} — Update tenant company info.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTenantRequest request)
        {
            var adminId = GetMainAdminUserId();
            if (adminId == null) return Forbid();

            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && t.MainAdminUserId == adminId.Value);

            if (tenant == null) return NotFound();

            // Update fields
            if (request.CompanyName != null) tenant.CompanyName = request.CompanyName;
            if (request.CompanyLogoUrl != null) tenant.CompanyLogoUrl = request.CompanyLogoUrl;
            if (request.CompanyWebsite != null) tenant.CompanyWebsite = request.CompanyWebsite;
            if (request.CompanyPhone != null) tenant.CompanyPhone = request.CompanyPhone;
            if (request.CompanyAddress != null) tenant.CompanyAddress = request.CompanyAddress;
            if (request.CompanyCountry != null) tenant.CompanyCountry = request.CompanyCountry;
            if (request.CompanyEmail != null) tenant.CompanyEmail = request.CompanyEmail;
            if (request.CompanyTagline != null) tenant.CompanyTagline = request.CompanyTagline;
            if (request.CompanyCity != null) tenant.CompanyCity = request.CompanyCity;
            if (request.CompanyPostalCode != null) tenant.CompanyPostalCode = request.CompanyPostalCode;
            if (request.CompanyState != null) tenant.CompanyState = request.CompanyState;
            if (request.TaxId != null) tenant.TaxId = request.TaxId;
            if (request.RegistrationNumber != null) tenant.RegistrationNumber = request.RegistrationNumber;
            if (request.ShareCapital != null) tenant.ShareCapital = request.ShareCapital;
            if (request.BankName != null) tenant.BankName = request.BankName;
            if (request.BankAccount != null) tenant.BankAccount = request.BankAccount;
            if (request.BankSwift != null) tenant.BankSwift = request.BankSwift;
            if (request.ReportFooterMessage != null) tenant.ReportFooterMessage = request.ReportFooterMessage;
            if (request.Industry != null) tenant.Industry = request.Industry;
            if (request.IsActive.HasValue) tenant.IsActive = request.IsActive.Value;
            tenant.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Refresh cache
            TenantSlugCache.Refresh(_context);

            return Ok(tenant);
        }

        /// <summary>
        /// DELETE /api/Tenants/{id} — Soft-delete (deactivate) a tenant.
        /// Cannot delete the default tenant.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var adminId = GetMainAdminUserId();
            if (adminId == null) return Forbid();

            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && t.MainAdminUserId == adminId.Value);

            if (tenant == null) return NotFound();

            if (tenant.IsDefault)
                return BadRequest(new { message = "Cannot delete the default company." });

            tenant.IsActive = false;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Refresh cache
            TenantSlugCache.Refresh(_context);

            return Ok(new { message = "Company deactivated successfully." });
        }

        /// <summary>
        /// POST /api/Tenants/{id}/set-default — Set a tenant as the default company.
        /// </summary>
        [HttpPost("{id}/set-default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var adminId = GetMainAdminUserId();
            if (adminId == null) return Forbid();

            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && t.MainAdminUserId == adminId.Value);

            if (tenant == null) return NotFound();

            // A deactivated company must never become the fallback company.
            if (!tenant.IsActive)
                return BadRequest(new { message = "Cannot set a deactivated company as default. Reactivate it first." });

            // Unset all other defaults for this admin
            var allTenants = await _context.Tenants
                .Where(t => t.MainAdminUserId == adminId.Value)
                .ToListAsync();

            foreach (var t in allTenants)
            {
                t.IsDefault = (t.Id == id);
                t.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TenantSlugCache.Refresh(_context);

            return Ok(new { message = "Default company updated." });
        }

        /// <summary>
        /// POST /api/Tenants/{id}/logo — Upload a logo image for a tenant.
        /// Accepts multipart/form-data with a "file" field. Saves the image to
        /// /uploads/company-logos/{tenantId}_{guid}{ext} and stores the relative
        /// path on Tenant.CompanyLogoUrl. Returns the updated tenant.
        /// </summary>
        [HttpPost("{id}/logo")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
        public async Task<IActionResult> UploadLogo(int id, IFormFile? file)
        {
            var adminId = GetMainAdminUserId();
            if (adminId == null) return Forbid();

            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && t.MainAdminUserId == adminId.Value);
            if (tenant == null) return NotFound();

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided." });

            var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
                return BadRequest(new { message = "Unsupported image type. Use PNG, JPG, GIF, WEBP or SVG." });

            // Uploads root: one level above the backend folder (same convention as DocumentsController)
            var backendDir = Directory.GetCurrentDirectory();
            var parentDir = Directory.GetParent(backendDir)?.FullName ?? backendDir;
            var uploadsDir = Path.Combine(parentDir, "uploads", "company-logos");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            var uniqueName = $"{tenant.Id}_{Guid.NewGuid():N}{ext}";
            var diskPath = Path.Combine(uploadsDir, uniqueName);

            await using (var stream = new FileStream(diskPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                await file.CopyToAsync(stream);
            }

            // Delete previous logo file when it was managed by this endpoint
            if (!string.IsNullOrEmpty(tenant.CompanyLogoUrl))
            {
                try
                {
                    var old = tenant.CompanyLogoUrl.TrimStart('/');
                    if (old.StartsWith("uploads/company-logos/", StringComparison.OrdinalIgnoreCase))
                    {
                        var oldDisk = Path.Combine(parentDir, old.Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(oldDisk)) System.IO.File.Delete(oldDisk);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete previous tenant logo for {TenantId}", tenant.Id);
                }
            }

            tenant.CompanyLogoUrl = $"uploads/company-logos/{uniqueName}";
            tenant.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TenantSlugCache.Refresh(_context);

            _logger.LogInformation("🖼️ Tenant {TenantId} logo updated by admin {AdminId}", tenant.Id, adminId.Value);
            return Ok(tenant);
        }
    }

    // ─── Request DTOs ───

    public class CreateTenantRequest
    {
        public string Slug { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyLogoUrl { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyCountry { get; set; }
        public string? Industry { get; set; }
        public string? CompanyEmail { get; set; }
        public string? CompanyTagline { get; set; }
        public string? CompanyCity { get; set; }
        public string? CompanyPostalCode { get; set; }
        public string? CompanyState { get; set; }
        public string? TaxId { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? ShareCapital { get; set; }
        public string? BankName { get; set; }
        public string? BankAccount { get; set; }
        public string? BankSwift { get; set; }
        public string? ReportFooterMessage { get; set; }
    }

    public class UpdateTenantRequest
    {
        public string? CompanyName { get; set; }
        public string? CompanyLogoUrl { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyCountry { get; set; }
        public string? Industry { get; set; }
        public string? CompanyEmail { get; set; }
        public string? CompanyTagline { get; set; }
        public string? CompanyCity { get; set; }
        public string? CompanyPostalCode { get; set; }
        public string? CompanyState { get; set; }
        public string? TaxId { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? ShareCapital { get; set; }
        public string? BankName { get; set; }
        public string? BankAccount { get; set; }
        public string? BankSwift { get; set; }
        public string? ReportFooterMessage { get; set; }
        public bool? IsActive { get; set; }
    }
}
