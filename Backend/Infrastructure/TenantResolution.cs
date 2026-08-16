namespace MyApi.Infrastructure;

/// <summary>
/// Shared tenant resolution for controllers that build their own DbContext.
/// Mirrors the scoped ApplicationDbContext registration in Program.cs:
/// middleware-resolved value first, X-Tenant header fallback, then the
/// default shared database (empty string).
/// </summary>
public static class TenantResolution
{
    public static string Resolve(HttpContext? context)
    {
        if (context == null) return string.Empty;

        if (context.Items.TryGetValue("Tenant", out var item)
            && item is string resolved
            && !string.IsNullOrWhiteSpace(resolved)
            && !string.Equals(resolved, TenantMiddleware.ViewAllSentinel, StringComparison.OrdinalIgnoreCase))
        {
            return resolved.Trim().ToLowerInvariant();
        }

        var header = context.Request.Headers[TenantMiddleware.TenantHeaderName].FirstOrDefault()?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(header)
            && !string.Equals(header, TenantMiddleware.ViewAllSentinel, StringComparison.OrdinalIgnoreCase))
        {
            return header;
        }

        return string.Empty;
    }
}
