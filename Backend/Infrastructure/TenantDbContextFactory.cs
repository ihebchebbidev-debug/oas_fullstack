using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using Npgsql;
using System.Collections.Concurrent;

namespace MyApi.Infrastructure;

/// <summary>
/// Factory that creates ApplicationDbContext instances with tenant-specific
/// connection strings. Registered as a singleton for performance — caches
/// normalized connection strings so they're only parsed once per tenant.
/// </summary>
public interface ITenantDbContextFactory
{
    ApplicationDbContext CreateDbContext(string? tenant);
    string GetConnectionString(string? tenant);
}

public class TenantDbContextFactory : ITenantDbContextFactory
{
    private readonly string _defaultConnectionString;
    private readonly ILogger<TenantDbContextFactory> _logger;
    private readonly bool _isDevelopment;

    // ── Connection string cache: tenant → normalized conn string ──
    private readonly ConcurrentDictionary<string, string> _connCache = new(StringComparer.OrdinalIgnoreCase);

    public TenantDbContextFactory(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<TenantDbContextFactory> logger)
    {
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();

        var raw = Environment.GetEnvironmentVariable("DATABASE_URL") ??
                  configuration.GetConnectionString("DefaultConnection") ?? "";

        _defaultConnectionString = NormalizePgUrl(raw);
    }

    /// <summary>
    /// Returns the final connection string for a tenant (cached).
    /// Returns the default connection string when tenant is null or unknown in development.
    /// In production, unknown tenants fail loudly instead of silently falling back.
    /// </summary>
    public string GetConnectionString(string? tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant) ||
            string.Equals(tenant, TenantMiddleware.ViewAllSentinel, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tenant, "default", StringComparison.OrdinalIgnoreCase))
        {
            return _defaultConnectionString;
        }

        return _connCache.GetOrAdd(tenant, t =>
        {
            var tenantConn = TenantConnectionResolver.GetConnectionString(t);
            if (!string.IsNullOrWhiteSpace(tenantConn))
            {
                _logger.LogInformation("🏢 Tenant '{Tenant}' → dedicated DB resolved via {EnvKey}", t, TenantConnectionResolver.GetEnvironmentVariableName(t));
                return NormalizePgUrl(tenantConn);
            }

            var envKey = TenantConnectionResolver.GetEnvironmentVariableName(t);
            // Check the DEFAULT DB's tenant directory (loaded eagerly at
            // startup) — that's where the master slug list lives. Looking
            // it up against `t`'s own DB would recurse: we're inside the
            // factory call that would build that DB's context.
            if (TenantSlugCache.HasTenant(TenantSlugCache.DefaultDbKey, t))
            {
                _logger.LogWarning("🏢 Tenant '{Tenant}' is valid but no dedicated DB is configured ({EnvKey}); using default shared DB", t, envKey);
                return _defaultConnectionString;
            }

            if (_isDevelopment)
            {
                _logger.LogWarning("🏢 Unknown tenant '{Tenant}' and no dedicated DB env var '{EnvKey}' found; using default DB in development", t, envKey);
                return _defaultConnectionString;
            }

            _logger.LogError("🏢 Unknown tenant '{Tenant}' with no dedicated DB env var '{EnvKey}'", t, envKey);
            throw new InvalidOperationException($"Unknown tenant '{t}'. Expected an active tenant slug or dedicated env var '{envKey}'.");
        });
    }

    public ApplicationDbContext CreateDbContext(string? tenant)
    {
        var connStr = GetConnectionString(tenant);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connStr, npgsql =>
            {
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                npgsql.CommandTimeout(30);
            });

        if (_isDevelopment)
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    public static string DescribeConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "(not set)";

        try
        {
            var builder = BuildConnectionStringBuilder(raw);
            var host = string.IsNullOrWhiteSpace(builder.Host) ? "unknown-host" : builder.Host;
            var db = string.IsNullOrWhiteSpace(builder.Database) ? "unknown-db" : builder.Database;
            return $"{host}/{db}";
        }
        catch
        {
            return "(unparseable)";
        }
    }

    /// <summary>
    /// Converts postgres:// URIs to Npgsql connection strings and applies pool settings.
    /// </summary>
    private static string NormalizePgUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        var builder = BuildConnectionStringBuilder(raw);
        builder.MaxPoolSize = 50;
        builder.MinPoolSize = 5;

        return builder.ToString();
    }

    private static NpgsqlConnectionStringBuilder BuildConnectionStringBuilder(string raw)
    {
        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(raw);
            var userInfo = uri.UserInfo?.Split(':', 2) ?? Array.Empty<string>();
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "",
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
                Database = uri.AbsolutePath?.TrimStart('/') ?? "",
                SslMode = SslMode.Require,
            };

            var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            foreach (var kv in queryParams)
            {
                try { builder[kv.Key] = kv.Value.ToString(); } catch { }
            }

            return builder;
        }

        return new NpgsqlConnectionStringBuilder(raw);
    }
}
