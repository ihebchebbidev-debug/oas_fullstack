using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.ShopFloorAuth.DTOs;

/// <summary>Never includes Pin (spec decision v12) — see OasUserDto for the console-auth equivalent.</summary>
public class OasOperatorDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Workspace { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? ScopeSiteId { get; set; }
    public Guid? ScopeZoneId { get; set; }
    public Guid? ScopeLineId { get; set; }
    public bool Interim { get; set; }
}

public class OasCreateOperatorRequestDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    [Required] public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = "operator";
    public string Workspace { get; set; } = "mobile";
    /// <summary>UsersPanel.tsx:25 `interim` field — temporary/agency worker flag. Persisted on oas_users.is_interim.</summary>
    public bool Interim { get; set; }
}

public class OasSetActiveRequestDto { public bool IsActive { get; set; } }
public class OasSetRoleRequestDto { [Required] public string Role { get; set; } = string.Empty; }
public class OasSetScopeRequestDto
{
    public Guid? ScopeSiteId { get; set; }
    public Guid? ScopeZoneId { get; set; }
    public Guid? ScopeLineId { get; set; }
}
