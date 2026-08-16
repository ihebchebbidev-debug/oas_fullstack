using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.ShopFloorAuth.DTOs;

public class OasSetupRequestDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    [Required] public string DisplayName { get; set; } = string.Empty;
}

public class OasLoginRequestDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class OasRefreshRequestDto
{
    [Required] public string RefreshToken { get; set; } = string.Empty;
}

public class OasChangePasswordRequestDto
{
    [Required] public string CurrentPassword { get; set; } = string.Empty;
    [Required, MinLength(8)] public string NewPassword { get; set; } = string.Empty;
}

public class OasAuthResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public OasUserDto? User { get; set; }
}

/// <summary>
/// Never includes Pin or PasswordHash (spec decision v12: GET-shaped
/// responses never return the PIN in plaintext — only the one-time
/// regenerate-pin response does, added in Lot 2).
/// </summary>
public class OasUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Workspace { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? ScopeSiteId { get; set; }
    public Guid? ScopeZoneId { get; set; }
    public Guid? ScopeLineId { get; set; }
}
