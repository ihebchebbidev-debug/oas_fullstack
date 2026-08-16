namespace MyApi.Modules.OAS.Common;

/// <summary>
/// Routing guard for every `api/oas/*` request (spec §1.2 bis points 4-5).
/// Reuses the socle's X-Tenant resolution as-is — never modifies
/// TenantMiddleware — and only acts on OAS routes:
///
///   • X-Tenant does not end in "oas" on an api/oas/* route  → 400 oas_tenant_required
///   • X-Tenant ends in "oas" but has no dedicated database   → 503 oas_tenant_not_provisioned
///   • Otherwise                                              → stash the slug for
///     the scoped OasDbContext factory (see OasModuleRegistration) and continue.
///
/// api/oas/health and api/oas/setup are exempt from the 400 (point 5).
/// Non-OAS routes are untouched — this middleware is a no-op for them.
/// </summary>
public class OasTenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OasTenantMiddleware> _logger;

    private static readonly string[] CrossSlugExemptPaths = { "/api/oas/health", "/api/oas/setup" };

    public OasTenantMiddleware(RequestDelegate next, ILogger<OasTenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOasDbContextFactory dbFactory)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!path.StartsWith("/api/oas", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var slug = context.Request.Headers[Infrastructure.TenantMiddleware.TenantHeaderName]
            .FirstOrDefault()?.Trim().ToLowerInvariant();

        var isExempt = CrossSlugExemptPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!OasTenant.IsOasSlug(slug))
        {
            if (isExempt)
            {
                await _next(context);
                return;
            }

            _logger.LogWarning("🏭 OAS-TENANT: rejected {Path} — X-Tenant '{Slug}' does not end in 'oas'", path, slug ?? "(none)");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "oas_tenant_required" });
            return;
        }

        try
        {
            // Pre-flight resolution: throws OasTenantNotProvisionedException if
            // no dedicated database is configured for this slug (fail-closed —
            // OAS never silently falls back to a shared database in Production).
            dbFactory.GetConnectionString(slug!);
        }
        catch (OasTenantNotProvisionedException ex)
        {
            _logger.LogError("🏭 OAS-TENANT: {Slug} not provisioned ({Path})", ex.Slug, path);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { error = "oas_tenant_not_provisioned", tenant = ex.Slug });
            return;
        }

        context.Items["OasSlug"] = slug;
        await _next(context);
    }
}

public static class OasTenantMiddlewareExtensions
{
    public static IApplicationBuilder UseOasTenantMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<OasTenantMiddleware>();
}
