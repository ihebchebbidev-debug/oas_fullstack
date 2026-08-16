using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.ShopFloorAuth.Models;

// `operator` is a reserved C# keyword — @operator is the standard escape;
// it does not change the identifier's actual name, so .ToString() still
// yields "operator" and the oas_app_role enum value in Postgres matches.
public enum OasAppRole { admin, supervisor, @operator }
public enum OasWorkspace { web, mobile, both }

/// <summary>
/// Autonomous OAS identity (spec §3.3, §8.1). No physical FK to the socle's
/// Users/profiles — source_user_id/source_tenant_id are informational
/// mapping columns only, populated by OasUserJitSyncService (spec §8.3).
/// password_hash is BCrypt and never serialized out; pin is intentionally
/// plaintext (spec §8.1 rationale) but — per plan decision v12 — is only
/// ever returned by the regenerate-pin endpoint, never by a GET.
/// </summary>
public class OasUser : IOasTenantEntity, IOasSoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TenantId { get; set; }

    public int? SourceUserId { get; set; }
    public int? SourceTenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>Shopfloor login identifier ("badge" in the frontend, `PinPanel`) — distinct from Email since a shop-floor tablet PIN pad favors a short code over a full email address.</summary>
    public string? EmployeeCode { get; set; }
    public string? PasswordHash { get; set; }
    public string? Pin { get; set; }
    public string? QrToken { get; set; }

    public OasAppRole Role { get; set; } = OasAppRole.@operator;
    public OasWorkspace Workspace { get; set; } = OasWorkspace.mobile;

    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }

    public Guid? ScopeSiteId { get; set; }
    public Guid? ScopeZoneId { get; set; }
    public Guid? ScopeLineId { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsInterim { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }

    // Refresh token bookkeeping (spec §8.2), mirrors the socle's pattern
    // (AuthService.cs: User.RefreshToken/TokenExpiresAt) but stored on the
    // OAS-native identity, never on the socle's User.
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }
}
