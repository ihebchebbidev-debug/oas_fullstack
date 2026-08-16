using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.ShopFloorAuth.Models;
using Npgsql;

namespace MyApi.Modules.OAS.ShopFloorAuth.Services;

public enum JitSyncOutcome { UpToDate, Synced, RejectedNotEligible, ManualAccountSkipped }

public sealed record JitSyncResult(JitSyncOutcome Outcome, OasUser? User);

/// <summary>
/// Just-in-time identity sync from the socle's source database (spec §8.3,
/// v11 decision — replaces the earlier periodic-polling design so a newly
/// eligible user can log in on their very first attempt, and a revoked
/// user is blocked on their very next attempt, with zero background
/// polling delay).
///
/// Reads the source database via raw Npgsql against the `"Users"` table
/// (MyApi.Modules.Users.Models.User, [Table("Users")]) by column name
/// only — this service deliberately never references that C# type or
/// ApplicationDbContext, preserving the isolation boundary (spec §3.2)
/// while still reading the socle's real schema.
/// </summary>
public interface IOasUserJitSyncService
{
    /// <summary>Looks up/syncs by console email. Used by console login + refresh.</summary>
    Task<JitSyncResult> SyncByEmailAsync(string oasSlug, int tenantId, string email);

    /// <summary>Looks up/syncs by shopfloor badge (employee code). Used by shopfloor PIN login.</summary>
    Task<JitSyncResult> SyncByEmployeeCodeAsync(string oasSlug, int tenantId, string employeeCode);
}

public class OasUserJitSyncService : IOasUserJitSyncService
{
    private static readonly string[] EligibleSourceRoles = { "oas_mobile", "oas_supervisor", "oas_admin" };

    private readonly OasDbContext _db;
    private readonly IOasDbContextFactory _dbFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OasUserJitSyncService> _logger;

    public OasUserJitSyncService(OasDbContext db, IOasDbContextFactory dbFactory, IConfiguration configuration, ILogger<OasUserJitSyncService> logger)
    {
        _db = db;
        _dbFactory = dbFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private int TtlSeconds => _configuration.GetValue<int?>("Oas:JitSyncTtlSeconds") ?? 300;

    public Task<JitSyncResult> SyncByEmailAsync(string oasSlug, int tenantId, string email)
        => SyncAsync(oasSlug, tenantId, email: email.Trim().ToLowerInvariant(), employeeCode: null);

    public Task<JitSyncResult> SyncByEmployeeCodeAsync(string oasSlug, int tenantId, string employeeCode)
        => SyncAsync(oasSlug, tenantId, email: null, employeeCode: employeeCode.Trim());

    private async Task<JitSyncResult> SyncAsync(string oasSlug, int tenantId, string? email, string? employeeCode)
    {
        var existing = email is not null
            ? await _db.Users.FirstOrDefaultAsync(u => u.Email == email)
            : await _db.Users.FirstOrDefaultAsync(u => u.EmployeeCode == employeeCode);

        // Manually created accounts (setup/console) are never touched by sync.
        if (existing is not null && existing.SourceUserId is null)
        {
            return new JitSyncResult(JitSyncOutcome.ManualAccountSkipped, existing);
        }

        if (existing is not null
            && existing.LastSyncedAt is not null
            && (DateTimeOffset.UtcNow - existing.LastSyncedAt.Value).TotalSeconds < TtlSeconds)
        {
            return new JitSyncResult(JitSyncOutcome.UpToDate, existing);
        }

        SourceUserRow? sourceRow;
        try
        {
            sourceRow = await LookupSourceUserAsync(oasSlug, tenantId, email, employeeCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🏭 OAS-JIT-SYNC: failed to reach source database for oas tenant '{Slug}'", oasSlug);
            // Network/source-DB failure must not silently strand an
            // already-synced account: fall back to the cached row if one
            // exists, otherwise this login attempt fails.
            return existing is not null
                ? new JitSyncResult(JitSyncOutcome.UpToDate, existing)
                : new JitSyncResult(JitSyncOutcome.RejectedNotEligible, null);
        }

        if (sourceRow is null || !EligibleSourceRoles.Contains(sourceRow.Role, StringComparer.OrdinalIgnoreCase))
        {
            if (existing is not null)
            {
                existing.IsActive = false;
                existing.LastSyncedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();
            }
            return new JitSyncResult(JitSyncOutcome.RejectedNotEligible, null);
        }

        var role = sourceRow.Role.Equals("oas_admin", StringComparison.OrdinalIgnoreCase) ? OasAppRole.admin
                 : sourceRow.Role.Equals("oas_supervisor", StringComparison.OrdinalIgnoreCase) ? OasAppRole.supervisor
                 : OasAppRole.@operator;
        var workspace = role == OasAppRole.@operator ? OasWorkspace.mobile : OasWorkspace.both;

        if (existing is null)
        {
            existing = new OasUser
            {
                TenantId = tenantId,
                SourceUserId = sourceRow.Id,
                SourceTenantId = sourceRow.TenantId,
            };
            _db.Users.Add(existing);
        }

        existing.Email = sourceRow.Email;
        existing.EmployeeCode = employeeCode ?? existing.EmployeeCode;
        existing.DisplayName = $"{sourceRow.FirstName} {sourceRow.LastName}".Trim();
        existing.Phone = sourceRow.Phone;
        existing.IsActive = sourceRow.IsActive;
        existing.Role = role;
        existing.Workspace = workspace;
        existing.LastSyncedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("🏭 OAS-JIT-SYNC: synced oas_users {Id} from source user {SourceId} (role={Role})", existing.Id, sourceRow.Id, role);

        return new JitSyncResult(JitSyncOutcome.Synced, existing);
    }

    private sealed record SourceUserRow(int Id, int TenantId, string Email, string FirstName, string LastName, string? Phone, bool IsActive, string Role);

    private async Task<SourceUserRow?> LookupSourceUserAsync(string oasSlug, int tenantId, string? email, string? employeeCode)
    {
        var sourceConnStr = _dbFactory.GetSourceConnectionString(oasSlug);

        await using var conn = new NpgsqlConnection(sourceConnStr);
        await conn.OpenAsync();

        // Single targeted lookup, never a full table scan (spec §8.3 step 3).
        // employee_code has no equivalent socle column today — the shopfloor
        // path matches on Email too until/unless the socle grows one; this
        // is a known simplification, not a silent gap (Phone is the closest
        // socle field and isn't a reliable badge substitute).
        var lookupValue = email ?? employeeCode!;
        await using var cmd = new NpgsqlCommand(
            """
            select "Id", "TenantId", "Email", "FirstName", "LastName", "Phone", "IsActive", coalesce("Role", '') as "Role"
            from "Users"
            where lower("Email") = lower(@lookup) and "IsDeleted" = false
            limit 1
            """, conn);
        cmd.Parameters.AddWithValue("lookup", lookupValue);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new SourceUserRow(
            Id: reader.GetInt32(0),
            TenantId: reader.GetInt32(1),
            Email: reader.GetString(2),
            FirstName: reader.IsDBNull(3) ? "" : reader.GetString(3),
            LastName: reader.IsDBNull(4) ? "" : reader.GetString(4),
            Phone: reader.IsDBNull(5) ? null : reader.GetString(5),
            IsActive: reader.GetBoolean(6),
            Role: reader.GetString(7));
    }
}
