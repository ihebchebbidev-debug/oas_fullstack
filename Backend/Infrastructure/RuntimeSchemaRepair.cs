using Microsoft.EntityFrameworkCore;
using MyApi.Data;

namespace MyApi.Infrastructure;

/// <summary>
/// Repairs additive schema drift (missing table or missing column) on demand when a
/// request fails with a Postgres "undefined column/table" error, so the very next
/// retry succeeds instead of the tenant staying broken until the next deploy.
/// Throttled per database to avoid DDL storms under load.
/// </summary>
public sealed class RuntimeSchemaRepair
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(15);

    private readonly DatabaseSchemaSynchronizer _synchronizer;
    private readonly ITenantDbContextFactory _factory;
    private readonly ILogger<RuntimeSchemaRepair> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> _lastRun = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeSchemaRepair(
        DatabaseSchemaSynchronizer synchronizer,
        ITenantDbContextFactory factory,
        ILogger<RuntimeSchemaRepair> logger)
    {
        _synchronizer = synchronizer;
        _factory = factory;
        _logger = logger;
    }

    public async Task<bool> TryRepairAsync(string? tenant, CancellationToken cancellationToken = default)
    {
        var key = string.IsNullOrWhiteSpace(tenant) ? "default" : tenant!;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_lastRun.TryGetValue(key, out var last) && DateTimeOffset.UtcNow - last < MinimumInterval)
                return false;
            _lastRun[key] = DateTimeOffset.UtcNow;
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            var connectionString = _factory.GetConnectionString(tenant);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            var result = await _synchronizer.SynchronizeAsync(key, connectionString, options, repair: true, cancellationToken);
            _logger.LogWarning(
                "Runtime schema repair for database {Database}: repaired={Repaired} unresolved={Unresolved}",
                key, result.RepairedColumns.Count, result.UnresolvedColumns.Count);
            return result.RepairedColumns.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Runtime schema repair failed for database {Database}", key);
            return false;
        }
    }
}
