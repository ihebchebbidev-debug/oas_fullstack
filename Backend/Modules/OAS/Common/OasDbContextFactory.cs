using Microsoft.EntityFrameworkCore;
using MyApi.Infrastructure;
using Npgsql;
using System.Collections.Concurrent;

namespace MyApi.Modules.OAS.Common;

/// <summary>Thrown when an *oas slug has no TENANT_&lt;SLUG&gt;_DATABASE_URL configured. Mapped to HTTP 503 (spec §1.2 bis point 4).</summary>
public class OasTenantNotProvisionedException : Exception
{
    public string Slug { get; }
    public OasTenantNotProvisionedException(string slug) : base($"OAS tenant '{slug}' is not provisioned.") => Slug = slug;
}

/// <summary>
/// Builds <see cref="OasDbContext"/> instances scoped to the *oas database
/// for the current request's slug. Reuses the socle's connection-string
/// resolution (<see cref="TenantConnectionResolver"/>, same env var
/// convention, same postgres:// normalization) — never reads DATABASE_URL
/// or any connection string directly (spec §1.2 bis point 2).
/// </summary>
public interface IOasDbContextFactory
{
    OasDbContext CreateDbContext(string oasSlug);
    string GetConnectionString(string oasSlug);

    /// <summary>Resolves the PARENT (non-oas) tenant's connection string for JIT user sync (spec §1.2 bis point 3, §8.3).</summary>
    string GetSourceConnectionString(string oasSlug);
}

public class OasDbContextFactory : IOasDbContextFactory
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<OasDbContextFactory> _logger;
    private readonly ConcurrentDictionary<string, string> _connCache = new(StringComparer.OrdinalIgnoreCase);

    // NpgsqlDataSource (not just a connection string) is what carries the
    // enum-type mappings from OasNpgsqlEnums — built once per slug and
    // reused, since building one re-queries Postgres for the enum OIDs.
    private readonly ConcurrentDictionary<string, NpgsqlDataSource> _dataSourceCache = new(StringComparer.OrdinalIgnoreCase);

    public OasDbContextFactory(IHostEnvironment environment, ILogger<OasDbContextFactory> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public string GetConnectionString(string oasSlug)
    {
        if (!MyApi.Modules.OAS.Common.OasTenant.IsOasSlug(oasSlug))
        {
            throw new InvalidOperationException($"'{oasSlug}' is not an *oas slug — this factory only resolves OAS tenant databases.");
        }

        return _connCache.GetOrAdd(oasSlug, slug =>
        {
            var raw = TenantConnectionResolver.GetConnectionString(slug);

            if (string.IsNullOrWhiteSpace(raw))
            {
                if (_environment.IsDevelopment())
                {
                    // Development-only fallback chain (spec §1.2 bis point 4):
                    // TENANT_DEVOAS_DATABASE_URL, then DATABASE_URL — never in
                    // Production, where an unprovisioned slug must 503, not
                    // silently share another tenant's database.
                    var devOas = TenantConnectionResolver.GetConnectionString("devoas");
                    if (!string.IsNullOrWhiteSpace(devOas))
                    {
                        _logger.LogWarning("🏭 OAS tenant '{Slug}' has no dedicated DB; Development fallback to TENANT_DEVOAS_DATABASE_URL", slug);
                        return NormalizePgUrl(devOas);
                    }

                    var fallback = Environment.GetEnvironmentVariable("DATABASE_URL");
                    if (!string.IsNullOrWhiteSpace(fallback))
                    {
                        _logger.LogWarning("🏭 OAS tenant '{Slug}' has no dedicated DB; Development fallback to DATABASE_URL", slug);
                        return NormalizePgUrl(fallback);
                    }
                }

                throw new OasTenantNotProvisionedException(slug);
            }

            return NormalizePgUrl(raw);
        });
    }

    public string GetSourceConnectionString(string oasSlug)
    {
        var baseSlug = MyApi.Modules.OAS.Common.OasTenant.BaseSlug(oasSlug);
        var raw = TenantConnectionResolver.GetConnectionString(baseSlug);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"No source connection configured for parent tenant '{baseSlug}' of OAS tenant '{oasSlug}' " +
                $"(expected {TenantConnectionResolver.GetEnvironmentVariableName(baseSlug)}).");
        }
        return NormalizePgUrl(raw);
    }

    public OasDbContext CreateDbContext(string oasSlug)
    {
        var connStr = GetConnectionString(oasSlug);
        var dataSource = _dataSourceCache.GetOrAdd(connStr, cs =>
            OasNpgsqlEnums.ConfigureMappings(new NpgsqlDataSourceBuilder(cs)).Build());

        var optionsBuilder = new DbContextOptionsBuilder<OasDbContext>()
            .UseNpgsql(dataSource, npgsql =>
            {
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
                npgsql.CommandTimeout(30);
            });

        if (_environment.IsDevelopment())
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }

        return new OasDbContext(optionsBuilder.Options);
    }

    private static string NormalizePgUrl(string raw)
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
                MaxPoolSize = 50,
                MinPoolSize = 5,
            };
            var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            foreach (var kv in queryParams)
            {
                try { builder[kv.Key] = kv.Value.ToString(); } catch { /* ignore unknown params */ }
            }
            return builder.ToString();
        }

        var b = new NpgsqlConnectionStringBuilder(raw) { MaxPoolSize = 50, MinPoolSize = 5 };
        return b.ToString();
    }
}
