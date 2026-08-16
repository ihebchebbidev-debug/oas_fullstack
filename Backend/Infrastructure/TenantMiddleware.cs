using System.Collections;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;

namespace MyApi.Infrastructure;

/// <summary>
/// Middleware that reads the X-Tenant header from the request,
/// resolves a per-tenant connection string, and reconfigures
/// the ApplicationDbContext for the current request scope.
///
/// If no header is provided, the default connection string from
/// DATABASE_URL / appsettings is used.
///
/// Special value "__all__" enables cross-company mode for MainAdminUsers.
/// Mutations require an additional X-Target-Tenant header specifying the
/// target tenant ID, so that writes are properly scoped.
/// </summary>
public class TenantMiddleware
{
    public const string TenantHeaderName = "X-Tenant";
    public const string TargetTenantHeaderName = "X-Target-Tenant";
    public const string ViewAllHeaderName = "X-View-All";
    public const string ViewAllSentinel = "__all__";

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    private static bool IsMainAdmin(HttpContext ctx)
    {
        var claims = ctx.User?.Claims;
        if (claims == null) return false;
        return claims.Any(c =>
            (string.Equals(c.Type, "UserType", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(c.Value, "MainAdminUser", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(c.Type, "user_type", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(c.Value, "MainAdminUser", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(c.Type, "login_type", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(c.Value, "admin", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Regular users (login_type=user) are pinned to ONE company (JWT
    /// tenant_id claim, data-table TenantId — 0 = default). They may only
    /// pass an X-Target-Tenant that matches their pinned company, unless
    /// their role grants settings.switch_company (JWT can_switch_company
    /// claim = "true"). MainAdminUsers are unrestricted.
    /// </summary>
    private static bool IsRegularUserAllowedToTarget(HttpContext ctx, int dataTenantId)
    {
        if (ctx.User?.Identity?.IsAuthenticated != true) return true;
        var loginType = ctx.User.FindFirst("login_type")?.Value;
        if (!string.Equals(loginType, "user", StringComparison.OrdinalIgnoreCase)) return true;

        var canSwitch = ctx.User.FindFirst("can_switch_company")?.Value;
        if (string.Equals(canSwitch, "true", StringComparison.OrdinalIgnoreCase)) return true;

        var claim = ctx.User.FindFirst("tenant_id")?.Value;
        // Legacy tokens (issued before this claim existed) cannot be validated
        // — allow them through so existing sessions don't 403 mid-flight.
        if (string.IsNullOrEmpty(claim)) return true;
        return int.TryParse(claim, out var boundTenantId) && boundTenantId == dataTenantId;
    }


    /// <summary>
    /// Endpoints that legitimately operate WITHOUT a row-level company
    /// selection — auth, tenant directory, profile/me, system logs, swagger,
    /// health checks. Everything else inside /api/* must have an active
    /// company once the user is authenticated (Fix #4).
    /// </summary>
    private static bool IsCompanyOptionalPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return true;
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Contains("/api/public", StringComparison.OrdinalIgnoreCase)) return true;
        return path.Contains("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/email-verification", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/twofactor", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/tenants", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/systemlogs", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/logs", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/profile", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/users/me", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/me", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/module-scope", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/health", StringComparison.OrdinalIgnoreCase)
            // Per-user offline hydration module toggles: a UI preference the app
            // reads right after login, before a company has been selected.
            || path.Contains("/api/offlinehydrationpreferences", StringComparison.OrdinalIgnoreCase)
            // Onboarding uploads: profile picture + company logo hit
            // /api/Documents/upload and /api/Upload BEFORE the first tenant is
            // created, so they must be reachable without an active company.
            || path.Contains("/api/documents/upload", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/upload", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/swagger", StringComparison.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenant = context.Request.Headers[TenantHeaderName].FirstOrDefault()?.Trim().ToLowerInvariant();
        var dbSlug = string.IsNullOrEmpty(tenant) ? null : tenant;

        // Lazily load the per-DB slug cache so X-Target-Tenant validation runs
        // against the SAME database EF will hit (Fix #1).
        try
        {
            TenantSlugCache.EnsureInitialized(dbSlug, context.RequestServices);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "🏢 TENANT-MIDDLEWARE: Could not initialize tenant cache for db='{Db}'", dbSlug ?? "default");
        }

        // ── Fix #2: explicit X-View-All header on a real subdomain ──
        // The picker keeps X-Tenant pinned to the subdomain (so the per-DB
        // selection stays correct) and signals "see everything inside this
        // DB" with X-View-All:true. Only MainAdminUser may opt in.
        var viewAllHeader = context.Request.Headers[ViewAllHeaderName].FirstOrDefault();
        var isExplicitViewAll = string.Equals(viewAllHeader, "true", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(viewAllHeader, "1", StringComparison.Ordinal);
        if (isExplicitViewAll)
        {
            if (!IsMainAdmin(context))
            {
                _logger.LogWarning("🚫 TENANT-MIDDLEWARE: Non-MainAdmin attempted X-View-All on db='{Db}'", dbSlug ?? "default");
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "Only MainAdminUser can use view-all mode" });
                return;
            }

            // Hardened write guard (parity with __all__ branch): in view-all mode
            // mutations MUST target a specific company via X-Target-Tenant, or
            // they would silently write at TenantId=-1 and corrupt scoping.
            var vaMethod = context.Request.Method.ToUpperInvariant();
            var vaIsMutation = vaMethod is "POST" or "PUT" or "PATCH" or "DELETE";
            if (vaIsMutation)
            {
                var vaPath = context.Request.Path.Value?.ToLowerInvariant() ?? "";
                var vaIsSafeEndpoint = vaPath.Contains("/api/tenants")
                    || vaPath.Contains("/api/auth")
                    || vaPath.Contains("/api/systemlogs")
                    || vaPath.Contains("/api/logs")
                    || vaPath.Contains("/api/module-scope");

                if (!vaIsSafeEndpoint)
                {
                    var vaTargetHeader = context.Request.Headers[TargetTenantHeaderName].FirstOrDefault();
                    if (string.IsNullOrEmpty(vaTargetHeader) || !int.TryParse(vaTargetHeader, out var vaTargetId))
                    {
                        _logger.LogWarning("🚫 TENANT-MIDDLEWARE: X-View-All mutation without valid X-Target-Tenant on db='{Db}', path={Path}", dbSlug ?? "default", vaPath);
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync(new { error = "X-Target-Tenant header with a valid tenant ID is required for mutations in view-all mode." });
                        return;
                    }
                    if (!TenantSlugCache.IsValidTenantId(dbSlug, vaTargetId))
                    {
                        _logger.LogWarning("🚫 TENANT-MIDDLEWARE: X-View-All X-Target-Tenant {TenantId} invalid/inactive on db='{Db}'", vaTargetId, dbSlug ?? "default");
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsJsonAsync(new { error = $"Target tenant {vaTargetId} does not exist or is inactive." });
                        return;
                    }

                    var vaDataTenantId = TenantSlugCache.ToDataTenantId(dbSlug, vaTargetId);
                    context.Items["Tenant"] = tenant ?? string.Empty;
                    context.Items["TenantId"] = vaDataTenantId;
                    context.Items["TenantViewAll"] = true;
                    context.Items["TenantTargetOverride"] = true;
                    _logger.LogDebug("🏢 TENANT-MIDDLEWARE: {Method} {Path} → VIEW-ALL mutation scoped to TenantId={TenantId} (header={Header})",
                        vaMethod, context.Request.Path, vaDataTenantId, vaTargetId);
                    await _next(context);
                    return;
                }
            }

            context.Items["Tenant"] = tenant ?? string.Empty;
            context.Items["TenantId"] = -1;
            context.Items["TenantViewAll"] = true;
            _logger.LogDebug("🏢 TENANT-MIDDLEWARE: VIEW-ALL on db='{Db}' (TenantId=-1, bypass row filter)", dbSlug ?? "default");
            await _next(context);
            return;
        }

        if (string.Equals(tenant, ViewAllSentinel, StringComparison.OrdinalIgnoreCase))
        {
            if (!IsMainAdmin(context))
            {
                _logger.LogWarning(
                    "🚫 TENANT-MIDDLEWARE: Non-MainAdmin attempted __all__ mode (IsAuthenticated={Auth}, ClaimCount={Count})",
                    context.User?.Identity?.IsAuthenticated ?? false,
                    context.User?.Claims?.Count() ?? 0);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "Only MainAdminUser can use view-all mode" });
                return;
            }

            var method = context.Request.Method.ToUpperInvariant();
            var isMutation = method is "POST" or "PUT" or "PATCH" or "DELETE";

            if (isMutation)
            {
                var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
                // System-level endpoints that don't require a target tenant in view-all mode:
                //  • /api/tenants — managing the tenants themselves
                //  • /api/auth   — login/logout/refresh
                //  • /api/systemlogs — global audit/error stream (no tenant scoping)
                //  • /api/logs   — alias for system logs
                var isSafeEndpoint = path.Contains("/api/tenants")
                    || path.Contains("/api/auth")
                    || path.Contains("/api/systemlogs")
                    || path.Contains("/api/logs");

                if (!isSafeEndpoint)
                {
                    var targetTenantHeader = context.Request.Headers[TargetTenantHeaderName].FirstOrDefault();

                    if (string.IsNullOrEmpty(targetTenantHeader) || !int.TryParse(targetTenantHeader, out var targetTenantId))
                    {
                        _logger.LogWarning("🚫 TENANT-MIDDLEWARE: Mutation in view-all mode without valid X-Target-Tenant header");
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync(new { error = "X-Target-Tenant header with a valid tenant ID is required for mutations in view-all mode." });
                        return;
                    }

                    var validTenantId = TenantSlugCache.IsValidTenantId(dbSlug, targetTenantId);
                    if (!validTenantId)
                    {
                        _logger.LogWarning("🚫 TENANT-MIDDLEWARE: X-Target-Tenant {TenantId} is invalid or inactive", targetTenantId);
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsJsonAsync(new { error = $"Target tenant {targetTenantId} does not exist or is inactive." });
                        return;
                    }

                    var dataTenantId = TenantSlugCache.ToDataTenantId(dbSlug, targetTenantId);
                    context.Items["Tenant"] = ViewAllSentinel;
                    context.Items["TenantId"] = dataTenantId;
                    context.Items["TenantViewAll"] = true;
                    context.Items["TenantTargetOverride"] = true;
                    _logger.LogDebug("🏢 TENANT-MIDDLEWARE: {Method} {Path} → VIEW-ALL mutation scoped to TenantId={TenantId} (header={Header})",
                        method, context.Request.Path, dataTenantId, targetTenantId);

                    await _next(context);
                    return;
                }
            }

            context.Items["Tenant"] = ViewAllSentinel;
            context.Items["TenantId"] = -1;
            context.Items["TenantViewAll"] = true;
            _logger.LogDebug("🏢 TENANT-MIDDLEWARE: Request {Method} {Path} → VIEW-ALL mode (MainAdmin)",
                context.Request.Method, context.Request.Path);
        }
        else if (!string.IsNullOrEmpty(tenant))
        {
            var knownTenant = TenantSlugCache.HasTenant(dbSlug, tenant);
            var hasDedicatedDb = TenantConnectionResolver.HasDedicatedConnectionString(tenant);

            if (!knownTenant && !hasDedicatedDb)
            {
                var envKey = TenantConnectionResolver.GetEnvironmentVariableName(tenant);
                _logger.LogWarning(
                    "🏢 TENANT-MIDDLEWARE: Unknown tenant '{Tenant}' will use the default shared DB. Configure '{EnvKey}' or activate the tenant slug to give it its own database.",
                    tenant, envKey);
            }

            context.Items["Tenant"] = tenant;
            // X-Tenant selects the app/database (krossier/demo/dev). It must NOT
            // automatically become the row-level company TenantId. Inside each
            // app database, rows default to company TenantId=0 unless the company
            // switcher sends X-Target-Tenant.
            var tenantId = 0;
            var hasTargetOverride = false;
            var targetTenantHeader = context.Request.Headers[TargetTenantHeaderName].FirstOrDefault();
            if (!string.IsNullOrEmpty(targetTenantHeader) && int.TryParse(targetTenantHeader, out var targetTenantId))
            {
                if (!TenantSlugCache.IsValidTenantId(dbSlug, targetTenantId))
                {
                    _logger.LogWarning("🚫 TENANT-MIDDLEWARE: X-Target-Tenant {TenantId} is invalid or inactive", targetTenantId);
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsJsonAsync(new { error = $"Target company {targetTenantId} does not exist or is inactive." });
                    return;
                }
                tenantId = TenantSlugCache.ToDataTenantId(dbSlug, targetTenantId);

                // Regular-user company-lock: a non-admin can only target the
                // company their account is bound to (JWT tenant_id claim)
                // unless their role grants settings.switch_company.
                if (!IsRegularUserAllowedToTarget(context, tenantId))
                {
                    _logger.LogWarning("🚫 TENANT-MIDDLEWARE: regular user attempted to switch to company {TenantId} without permission", tenantId);
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new { error = "You do not have permission to switch companies." });
                    return;
                }

                context.Items["TenantTargetOverride"] = true;
                hasTargetOverride = true;
            }
            context.Items["TenantId"] = tenantId;

            _logger.LogDebug("🏢 TENANT-MIDDLEWARE: Request {Method} {Path} → app tenant='{Tenant}' (CompanyTenantId={TenantId}, KnownTenant={KnownTenant}, DedicatedDb={DedicatedDb})",
                context.Request.Method, context.Request.Path, tenant, tenantId, knownTenant, hasDedicatedDb);

            // Fix #4: authenticated user on a data endpoint without an active
            // company → fail fast instead of silently serving TenantId=0 rows.
            if (!hasTargetOverride
                && (context.User?.Identity?.IsAuthenticated ?? false)
                && !IsCompanyOptionalPath(context.Request.Path.Value ?? string.Empty))
            {
                _logger.LogWarning("🚫 TENANT-MIDDLEWARE: Authenticated request to {Path} without active company on db='{Db}'",
                    context.Request.Path, dbSlug);
                context.Response.StatusCode = 428; // Precondition Required
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "company_required",
                    message = "Select an active company before calling this endpoint."
                });
                return;
            }
        }
        else
        {
            // No X-Tenant header (preview/dev hosts) — still honor X-Target-Tenant
            // so company-row scoping works the same way it does on real subdomains.
            var tenantId = 0;
            var hasTargetOverride = false;
            var targetTenantHeader = context.Request.Headers[TargetTenantHeaderName].FirstOrDefault();
            if (!string.IsNullOrEmpty(targetTenantHeader) && int.TryParse(targetTenantHeader, out var targetTenantId))
            {
                if (!TenantSlugCache.IsValidTenantId(dbSlug, targetTenantId))
                {
                    _logger.LogWarning("🚫 TENANT-MIDDLEWARE: X-Target-Tenant {TenantId} is invalid or inactive", targetTenantId);
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsJsonAsync(new { error = $"Target company {targetTenantId} does not exist or is inactive." });
                    return;
                }
                tenantId = TenantSlugCache.ToDataTenantId(dbSlug, targetTenantId);

                if (!IsRegularUserAllowedToTarget(context, tenantId))
                {
                    _logger.LogWarning("🚫 TENANT-MIDDLEWARE: regular user attempted to switch to company {TenantId} without permission", tenantId);
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new { error = "You do not have permission to switch companies." });
                    return;
                }

                context.Items["TenantTargetOverride"] = true;
                hasTargetOverride = true;
            }

            context.Items["TenantId"] = tenantId;

            // Fix #4 (preview/dev hosts with no X-Tenant): same guard.
            if (!hasTargetOverride
                && (context.User?.Identity?.IsAuthenticated ?? false)
                && !IsCompanyOptionalPath(context.Request.Path.Value ?? string.Empty))
            {
                _logger.LogWarning("🚫 TENANT-MIDDLEWARE: Authenticated request to {Path} without active company (no X-Tenant)",
                    context.Request.Path);
                context.Response.StatusCode = 428;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "company_required",
                    message = "Select an active company before calling this endpoint."
                });
                return;
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Resolves connection strings per tenant.
/// Checks (in order):
///   1. In-memory dictionary (for hardcoded tenants)
///   2. Environment variable TENANT_{NAME}_DATABASE_URL
///   3. Returns null → caller may use default connection
/// </summary>
public static class TenantConnectionResolver
{
    private const string TenantDatabasePrefix = "TENANT_";
    private const string TenantDatabaseSuffix = "_DATABASE_URL";

    private static readonly Dictionary<string, string> _tenantConnections = new(StringComparer.OrdinalIgnoreCase)
    {
        // Add tenant mappings here only if you intentionally want to hardcode them.
        // Prefer Render env vars like TENANT_KROSSIER_DATABASE_URL instead.
    };

    public sealed record ConfiguredTenantConnection(string Tenant, string Source, string EnvironmentVariable, string ConnectionString);

    public static string GetEnvironmentVariableName(string tenant)
        => $"{TenantDatabasePrefix}{NormalizeTenantForEnvironmentKey(tenant)}{TenantDatabaseSuffix}";

    private static string NormalizeTenantForEnvironmentKey(string tenant)
        => new string(tenant.Trim().ToUpperInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

    public static string? GetConnectionString(string? tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant) || string.Equals(tenant, TenantMiddleware.ViewAllSentinel, StringComparison.OrdinalIgnoreCase))
            return null;

        if (_tenantConnections.TryGetValue(tenant, out var connStr) && !string.IsNullOrWhiteSpace(connStr))
            return connStr;

        var envKey = GetEnvironmentVariableName(tenant);
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue;

        return null;
    }

    public static bool HasDedicatedConnectionString(string? tenant)
        => !string.IsNullOrWhiteSpace(GetConnectionString(tenant));

    public static IReadOnlyList<ConfiguredTenantConnection> GetConfiguredTenantConnections()
    {
        var results = new Dictionary<string, ConfiguredTenantConnection>(StringComparer.OrdinalIgnoreCase);

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string key || entry.Value is not string value) continue;
            if (!key.StartsWith(TenantDatabasePrefix, StringComparison.OrdinalIgnoreCase) ||
                !key.EndsWith(TenantDatabaseSuffix, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var tenant = key[TenantDatabasePrefix.Length..^TenantDatabaseSuffix.Length].ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(tenant)) continue;

            results[tenant] = new ConfiguredTenantConnection(tenant, "environment", key, value);
        }

        foreach (var kv in _tenantConnections)
        {
            if (string.IsNullOrWhiteSpace(kv.Value)) continue;
            var tenant = kv.Key.Trim().ToLowerInvariant();
            results[tenant] = new ConfiguredTenantConnection(tenant, "code", GetEnvironmentVariableName(tenant), kv.Value);
        }

        return results.Values.OrderBy(x => x.Tenant).ToList();
    }
}

/// <summary>
/// Resolves a tenant-specific public files base URL.
/// </summary>
public static class TenantPublicUrlResolver
{
    private static readonly Dictionary<string, string> _tenantPublicBases = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    public static string? GetPublicBaseUrl(string? tenant)
    {
        if (string.IsNullOrEmpty(tenant)) return null;

        if (_tenantPublicBases.TryGetValue(tenant, out var baseUrl)) return baseUrl;

        var envKey = $"TENANT_{tenant.ToUpperInvariant()}_PUBLIC_BASE_URL";
        var envVal = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrEmpty(envVal)) return envVal;

        return null;
    }
}
