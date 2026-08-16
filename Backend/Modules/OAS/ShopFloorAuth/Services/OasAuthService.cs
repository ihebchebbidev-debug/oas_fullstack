using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.ShopFloorAuth.DTOs;
using MyApi.Modules.OAS.ShopFloorAuth.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MyApi.Modules.OAS.ShopFloorAuth.Services;

/// <summary>
/// Console auth (setup + email/password login) — spec §8.2. Every login and
/// refresh triggers OasUserJitSyncService first (spec §8.3, v11 decision)
/// so a newly source-eligible user can log in on the first attempt and a
/// revoked one is blocked on the next — manually created accounts
/// (source_user_id = null, from /oas/setup) skip the sync flow entirely.
///
/// Account lockout (5 failed attempts → 15 min, spec §8.1) applies to
/// password verification only — a JIT-sync rejection is a separate,
/// earlier failure path (401 invalid_credentials, spec §8.3 step 5).
/// </summary>
public class OasAuthService : IOasAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(8);

    private readonly OasDbContext _db;
    private readonly IOasUserJitSyncService _jitSync;
    private readonly IOasTokenService _tokenService;
    private readonly ILogger<OasAuthService> _logger;
    private readonly string _oasSlug;

    public OasAuthService(OasDbContext db, IOasUserJitSyncService jitSync, IOasTokenService tokenService, ILogger<OasAuthService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _jitSync = jitSync;
        _tokenService = tokenService;
        _logger = logger;
        _oasSlug = httpContextAccessor.HttpContext?.Items["OasSlug"] as string
            ?? throw new InvalidOperationException("OasSlug not resolved on HttpContext — OasTenantMiddleware must run before this service is used.");
    }

    public async Task<OasAuthResponseDto> SetupAsync(string oasSlug, int tenantId, OasSetupRequestDto request)
    {
        // Tenant + soft-delete scoping already applied by OasDbContext's
        // combined query filter (SetTenantId is set to `tenantId` for this
        // request) — no IgnoreQueryFilters() here, or this would check for
        // an existing admin across every tenant sharing this database.
        var adminExists = await _db.Users.AnyAsync(u => u.Role == OasAppRole.admin);

        if (adminExists)
        {
            return new OasAuthResponseDto { Success = false, Message = "An admin already exists for this tenant." };
        }

        var user = new OasUser
        {
            TenantId = tenantId,
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BCrypt.Net.BCrypt.GenerateSalt(12)),
            DisplayName = request.DisplayName,
            Role = OasAppRole.admin,
            Workspace = OasWorkspace.web,
            IsActive = true,
            SourceUserId = null,
            SourceTenantId = null,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("🏭 OAS-SETUP: first admin created for oas tenant '{Slug}' (tenantId={TenantId})", _oasSlug, tenantId);

        var (accessToken, refreshToken, expiresAt) = await IssueTokensAsync(user);
        return new OasAuthResponseDto
        {
            Success = true,
            Message = "Setup complete",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = ToDto(user),
        };
    }

    public async Task<OasAuthResponseDto> LoginAsync(string oasSlug, int tenantId, OasLoginRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Same response whether the account doesn't exist or the password is
        // wrong — no account-enumeration signal (spec §8.3 step 5).
        var genericFailure = new OasAuthResponseDto { Success = false, Message = "Invalid credentials." };

        var syncResult = await _jitSync.SyncByEmailAsync(oasSlug, tenantId, email);
        if (syncResult.Outcome == JitSyncOutcome.RejectedNotEligible) return genericFailure;

        var user = syncResult.User ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return genericFailure;

        if (!user.IsActive) return new OasAuthResponseDto { Success = false, Message = "Account is inactive." };

        if (user.LockedUntil is not null && user.LockedUntil > DateTimeOffset.UtcNow)
        {
            return new OasAuthResponseDto { Success = false, Message = "Account is temporarily locked. Try again later." };
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await RegisterFailedAttemptAsync(user);
            return genericFailure;
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTimeOffset.UtcNow;

        var (accessToken, refreshToken, expiresAt) = await IssueTokensAsync(user);

        return new OasAuthResponseDto
        {
            Success = true,
            Message = "Login successful",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = ToDto(user),
        };
    }

    private async Task RegisterFailedAttemptAsync(OasUser user)
    {
        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= MaxFailedAttempts)
        {
            user.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
            _logger.LogWarning("🏭 OAS-AUTH: user {UserId} locked out after {Attempts} failed attempts", user.Id, user.FailedLoginAttempts);
        }
        await _db.SaveChangesAsync();
    }

    public async Task<OasUserDto?> GetCurrentUserAsync(Guid oasUserId)
    {
        var user = await _db.Users.FindAsync(oasUserId);
        return user is null ? null : ToDto(user);
    }

    public async Task<OasAuthResponseDto> RefreshAsync(OasRefreshRequestDto request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);
        if (user is null || user.RefreshTokenExpiresAt is null || user.RefreshTokenExpiresAt < DateTimeOffset.UtcNow)
        {
            return new OasAuthResponseDto { Success = false, Message = "Invalid or expired refresh token." };
        }

        // Re-sync before honoring the refresh (spec §8.3): a socle-side
        // revocation cuts access at the next refresh, not just the next
        // fresh login.
        if (user.SourceUserId is not null)
        {
            var syncResult = await _jitSync.SyncByEmailAsync(_oasSlug, user.TenantId, user.Email);
            if (syncResult.Outcome == JitSyncOutcome.RejectedNotEligible)
            {
                return new OasAuthResponseDto { Success = false, Message = "Access has been revoked." };
            }
            user = syncResult.User ?? user;
        }

        if (!user.IsActive)
        {
            return new OasAuthResponseDto { Success = false, Message = "Account is inactive." };
        }

        var (accessToken, refreshToken, expiresAt) = await IssueTokensAsync(user);
        return new OasAuthResponseDto
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = ToDto(user),
        };
    }

    public async Task<bool> LogoutAsync(Guid oasUserId)
    {
        var user = await _db.Users.FindAsync(oasUserId);
        if (user is null) return false;
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<OasAuthResponseDto> ChangePasswordAsync(Guid oasUserId, OasChangePasswordRequestDto request)
    {
        var user = await _db.Users.FindAsync(oasUserId);
        if (user is null) return new OasAuthResponseDto { Success = false, Message = "User not found." };

        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return new OasAuthResponseDto { Success = false, Message = "Current password is incorrect." };
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, BCrypt.Net.BCrypt.GenerateSalt(12));
        await _db.SaveChangesAsync();
        return new OasAuthResponseDto { Success = true, Message = "Password changed." };
    }

    private async Task<(string accessToken, string refreshToken, DateTimeOffset expiresAt)> IssueTokensAsync(OasUser user)
    {
        var (accessToken, refreshToken, expiresAt) = _tokenService.IssueTokens(user);
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
        await _db.SaveChangesAsync();
        return (accessToken, refreshToken, expiresAt);
    }

    private static OasUserDto ToDto(OasUser u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        DisplayName = u.DisplayName,
        Role = u.Role.ToString(),
        Workspace = u.Workspace.ToString(),
        IsActive = u.IsActive,
        ScopeSiteId = u.ScopeSiteId,
        ScopeZoneId = u.ScopeZoneId,
        ScopeLineId = u.ScopeLineId,
    };
}
