using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.ShopFloorAuth.DTOs;
using MyApi.Modules.OAS.ShopFloorAuth.Models;

namespace MyApi.Modules.OAS.ShopFloorAuth.Services;

/// <summary>Operators CRUD (spec §6.1 "Opérateurs", 6 endpoints — the 6th, regenerate-pin, is IOasShopFloorAuthService.RegeneratePinAsync shared with /shopfloor/pin/regenerate).</summary>
public class OasOperatorService : IOasOperatorService
{
    private readonly OasDbContext _db;
    private readonly System.Security.Claims.ClaimsPrincipal? _user;

    public OasOperatorService(OasDbContext db, Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _user = httpContextAccessor.HttpContext?.User;
    }

    private Guid? CallerScopeClaim(string type)
    {
        var claim = _user?.FindFirst(type)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public async Task<IReadOnlyList<OasOperatorDto>> SearchAsync(int tenantId, string? q, Guid? scopeSiteId, Guid? scopeZoneId, Guid? scopeLineId)
    {
        var query = _db.Users.AsQueryable();

        // BL-012: the CALLER's own perimeter is a floor, independent of the
        // scopeSiteId/scopeZoneId/scopeLineId query params above (which
        // filter by the TARGET users' assignment, e.g. "show me who's on
        // line X") — a scoped supervisor previously saw the entire tenant's
        // directory here regardless of their own assignment.
        var callerSiteId = CallerScopeClaim("oas_scope_site_id");
        var callerZoneId = CallerScopeClaim("oas_scope_zone_id");
        var callerLineId = CallerScopeClaim("oas_scope_line_id");
        if (callerLineId is not null) query = query.Where(u => u.ScopeLineId == callerLineId);
        else if (callerZoneId is not null) query = query.Where(u => u.ScopeZoneId == callerZoneId);
        else if (callerSiteId is not null) query = query.Where(u => u.ScopeSiteId == callerSiteId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim().ToLowerInvariant();
            query = query.Where(u =>
                (u.DisplayName != null && u.DisplayName.ToLower().Contains(needle))
                || u.Email.ToLower().Contains(needle)
                || (u.EmployeeCode != null && u.EmployeeCode.ToLower().Contains(needle)));
        }

        if (scopeLineId is not null) query = query.Where(u => u.ScopeLineId == scopeLineId);
        else if (scopeZoneId is not null) query = query.Where(u => u.ScopeZoneId == scopeZoneId);
        else if (scopeSiteId is not null) query = query.Where(u => u.ScopeSiteId == scopeSiteId);

        var rows = await query.OrderBy(u => u.DisplayName).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<(bool success, string? error, OasOperatorDto? operatorDto)> CreateAsync(int tenantId, OasCreateOperatorRequestDto request, string callerRole)
    {
        if (!Enum.TryParse<OasAppRole>(request.Role, ignoreCase: true, out var role))
        {
            return (false, "invalid_role", null);
        }
        if (!Enum.TryParse<OasWorkspace>(request.Workspace, ignoreCase: true, out var workspace))
        {
            return (false, "invalid_workspace", null);
        }
        // This action is [OasAuthorize(Roles = "admin,supervisor")] — but SetRole
        // (promoting an EXISTING user to admin) is admin-only, and creating a
        // brand-new user already AT role=admin bypassed that restriction entirely
        // since nothing here checked the caller's own role. Mirror SetRole's rule.
        if (role == OasAppRole.admin && !string.Equals(callerRole, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "admin_role_requires_admin_caller", null);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return (false, "email_already_exists", null);
        }

        var user = new OasUser
        {
            TenantId = tenantId,
            Email = email,
            EmployeeCode = request.EmployeeCode?.Trim(),
            DisplayName = request.DisplayName,
            Phone = request.Phone,
            Role = role,
            Workspace = workspace,
            IsInterim = request.Interim,
            IsActive = true,
            SourceUserId = null, // manually created, never touched by JIT sync
            // Only web-workspace accounts (admin/supervisor) sign in with a
            // password — mobile authenticates by PIN (RegeneratePinAsync).
            // Without this, a console account created here had PasswordHash
            // permanently null and could never sign in at all.
            PasswordHash = workspace == OasWorkspace.web && !string.IsNullOrEmpty(request.Password)
                ? BCrypt.Net.BCrypt.HashPassword(request.Password, BCrypt.Net.BCrypt.GenerateSalt(12))
                : null,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return (true, null, ToDto(user));
    }

    public async Task<(bool success, string? error)> SetActiveAsync(int tenantId, Guid id, bool isActive, string callerRole)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return (false, "not_found");
        // A supervisor could otherwise deactivate (or reactivate) an admin
        // account — the same "supervisor touches an admin" gap already
        // closed on Create/SetRole for this controller.
        if (user.Role == OasAppRole.admin && !string.Equals(callerRole, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "admin_target_requires_admin_caller");
        }
        user.IsActive = isActive;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool success, string? error)> SetRoleAsync(int tenantId, Guid id, string role, string callerRole)
    {
        if (!Enum.TryParse<OasAppRole>(role, ignoreCase: true, out var parsedRole))
        {
            return (false, "invalid_role");
        }
        var user = await _db.Users.FindAsync(id);
        if (user is null) return (false, "not_found");
        var isAdminCaller = string.Equals(callerRole, "admin", StringComparison.OrdinalIgnoreCase);
        // Promoting TO admin, or changing an EXISTING admin's role at all,
        // both require an admin caller — mirrors CreateAsync's rule so a
        // supervisor can't escalate via either path.
        if ((parsedRole == OasAppRole.admin || user.Role == OasAppRole.admin) && !isAdminCaller)
        {
            return (false, "admin_role_requires_admin_caller");
        }
        user.Role = parsedRole;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> SetScopeAsync(int tenantId, Guid id, OasSetScopeRequestDto request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;
        user.ScopeSiteId = request.ScopeSiteId;
        user.ScopeZoneId = request.ScopeZoneId;
        user.ScopeLineId = request.ScopeLineId;
        await _db.SaveChangesAsync();
        return true;
    }

    private static OasOperatorDto ToDto(OasUser u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        EmployeeCode = u.EmployeeCode,
        DisplayName = u.DisplayName,
        Phone = u.Phone,
        Role = u.Role.ToString(),
        Workspace = u.Workspace.ToString(),
        IsActive = u.IsActive,
        ScopeSiteId = u.ScopeSiteId,
        ScopeZoneId = u.ScopeZoneId,
        ScopeLineId = u.ScopeLineId,
        Interim = u.IsInterim,
    };
}
