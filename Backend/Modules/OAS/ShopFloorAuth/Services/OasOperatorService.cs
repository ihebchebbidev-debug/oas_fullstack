using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.ShopFloorAuth.DTOs;
using MyApi.Modules.OAS.ShopFloorAuth.Models;

namespace MyApi.Modules.OAS.ShopFloorAuth.Services;

/// <summary>Operators CRUD (spec §6.1 "Opérateurs", 6 endpoints — the 6th, regenerate-pin, is IOasShopFloorAuthService.RegeneratePinAsync shared with /shopfloor/pin/regenerate).</summary>
public class OasOperatorService : IOasOperatorService
{
    private readonly OasDbContext _db;

    public OasOperatorService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasOperatorDto>> SearchAsync(int tenantId, string? q, Guid? scopeSiteId, Guid? scopeZoneId, Guid? scopeLineId)
    {
        var query = _db.Users.AsQueryable();

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

    public async Task<(bool success, string? error, OasOperatorDto? operatorDto)> CreateAsync(int tenantId, OasCreateOperatorRequestDto request)
    {
        if (!Enum.TryParse<OasAppRole>(request.Role, ignoreCase: true, out var role))
        {
            return (false, "invalid_role", null);
        }
        if (!Enum.TryParse<OasWorkspace>(request.Workspace, ignoreCase: true, out var workspace))
        {
            return (false, "invalid_workspace", null);
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
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return (true, null, ToDto(user));
    }

    public async Task<bool> SetActiveAsync(int tenantId, Guid id, bool isActive)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;
        user.IsActive = isActive;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool success, string? error)> SetRoleAsync(int tenantId, Guid id, string role)
    {
        if (!Enum.TryParse<OasAppRole>(role, ignoreCase: true, out var parsedRole))
        {
            return (false, "invalid_role");
        }
        var user = await _db.Users.FindAsync(id);
        if (user is null) return (false, "not_found");
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
