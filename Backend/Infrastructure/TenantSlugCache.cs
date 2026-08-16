using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyApi.Data;

namespace MyApi.Infrastructure;

/// <summary>
/// PER-DATABASE in-memory cache mapping tenant slugs to their numeric TenantId.
///
/// Multi-DB design — each X-Tenant subdomain (krossier, demo, dev, …) points
/// at its own Postgres database. Companies live INSIDE each database, so the
/// slug → id / id-validity tables MUST be scoped per database. A single static
/// cache (the previous design) loaded krossier's company ids against the
/// default DB and then rejected them on krossier's DB as "invalid", or vice
/// versa. We now keep one PerDb entry per X-Tenant slug ("__default__" for the
/// fallback DB) and load it lazily on first request.
///
/// IMPORTANT: TenantId in data tables does NOT equal Tenant.Id.
/// - TenantId = 0 means "default tenant" (existing data before multi-tenancy)
/// - TenantId = Tenant.Id for all other tenants
/// - The "default" tenant (Slug="default", IsDefault=true) maps to TenantId = 0
/// </summary>
public static class TenantSlugCache
{
    public const string DefaultDbKey = "__default__";

    private sealed class PerDb
    {
        public readonly ConcurrentDictionary<string, int> SlugToId = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<int> RealIds = new();
        public readonly HashSet<int> DataIds = new();
        public volatile bool Initialized;
        public readonly object Lock = new();
    }

    private static readonly ConcurrentDictionary<string, PerDb> _caches =
        new(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeDbKey(string? dbSlug)
    {
        if (string.IsNullOrWhiteSpace(dbSlug) ||
            string.Equals(dbSlug, TenantMiddleware.ViewAllSentinel, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dbSlug, "default", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultDbKey;
        }
        return dbSlug.Trim().ToLowerInvariant();
    }

    private static PerDb GetOrCreate(string dbKey) => _caches.GetOrAdd(dbKey, _ => new PerDb());

    private static void LoadInto(PerDb entry, ApplicationDbContext db, string dbKey)
    {
        try
        {
            var tenants = db.Tenants
                .Where(t => t.IsActive)
                .Select(t => new { t.Slug, t.Id, t.IsDefault })
                .ToList();

            entry.SlugToId.Clear();
            entry.RealIds.Clear();
            entry.DataIds.Clear();

            foreach (var t in tenants)
            {
                var dataId = t.IsDefault ? 0 : t.Id;
                entry.SlugToId[t.Slug] = dataId;
                entry.RealIds.Add(t.Id);
                entry.DataIds.Add(dataId);
            }
        }
        catch (Exception ex)
        {
            var connStr = db.Database.GetConnectionString() ?? "";
            string host = "unknown", name = "unknown";
            try
            {
                var csb = new Npgsql.NpgsqlConnectionStringBuilder(connStr);
                host = csb.Host ?? "unknown";
                name = csb.Database ?? "unknown";
            }
            catch { }

            if (ex is Npgsql.PostgresException pg)
            {
                Console.WriteLine($"[TenantSlugCache] ❌ dbKey='{dbKey}' host='{host}' db='{name}' SqlState={pg.SqlState} Message={pg.MessageText}");
            }
            else
            {
                Console.WriteLine($"[TenantSlugCache] ❌ dbKey='{dbKey}' host='{host}' db='{name}' {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Lazily load the per-DB cache for the given X-Tenant slug. Idempotent
    /// and thread-safe. Creates a short-lived DbContext using the tenant
    /// factory (so the load runs against the RIGHT database).
    /// </summary>
    public static void EnsureInitialized(string? dbSlug, IServiceProvider services)
    {
        var key = NormalizeDbKey(dbSlug);
        var entry = GetOrCreate(key);
        if (entry.Initialized) return;
        lock (entry.Lock)
        {
            if (entry.Initialized) return;
            ApplicationDbContext? db = null;
            try
            {
                if (key == DefaultDbKey)
                {
                    var options = services.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
                    db = new ApplicationDbContext(options);
                }
                else
                {
                    var factory = services.GetRequiredService<ITenantDbContextFactory>();
                    db = factory.CreateDbContext(key);
                }
                LoadInto(entry, db, key);
                entry.Initialized = true;
            }
            finally
            {
                db?.Dispose();
            }
        }
    }

    /// <summary>
    /// Back-compat: pre-load the cache for the DEFAULT DB at startup. The
    /// per-tenant DBs initialize lazily on their first request.
    /// </summary>
    public static void Initialize(ApplicationDbContext db)
    {
        var entry = GetOrCreate(DefaultDbKey);
        lock (entry.Lock)
        {
            LoadInto(entry, db, DefaultDbKey);
            entry.Initialized = true;
        }
    }

    /// <summary>
    /// Refresh the cache for the database that <paramref name="db"/> is bound
    /// to. The DbContext doesn't know its slug, so we conservatively
    /// invalidate ALL caches — each is small and reloads on the next request.
    /// </summary>
    public static void Refresh(ApplicationDbContext db)
    {
        foreach (var entry in _caches.Values)
        {
            lock (entry.Lock) entry.Initialized = false;
        }
        // Eagerly re-populate the DB the caller is currently on, since they
        // probably want to see the change reflected in the current response.
        var current = GetOrCreate(DefaultDbKey);
        lock (current.Lock)
        {
            LoadInto(current, db, "current");
            // Don't mark Initialized=true for DefaultDbKey unless the caller
            // really is on the default DB — instead let the next request
            // re-initialize against the right one.
        }
    }

    /// <summary>Refresh a single DB cache by slug.</summary>
    public static void Refresh(string? dbSlug, IServiceProvider services)
    {
        var key = NormalizeDbKey(dbSlug);
        if (_caches.TryGetValue(key, out var entry))
        {
            lock (entry.Lock) entry.Initialized = false;
        }
        EnsureInitialized(dbSlug, services);
    }

    public static int GetTenantId(string? dbSlug, string? slug)
    {
        if (string.IsNullOrEmpty(slug)) return 0;
        var key = NormalizeDbKey(dbSlug);
        if (!_caches.TryGetValue(key, out var entry)) return 0;
        return entry.SlugToId.TryGetValue(slug, out var id) ? id : 0;
    }

    public static bool HasTenant(string? dbSlug, string slug)
    {
        var key = NormalizeDbKey(dbSlug);
        return _caches.TryGetValue(key, out var entry) && entry.SlugToId.ContainsKey(slug);
    }

    public static bool IsValidTenantId(string? dbSlug, int tenantId)
    {
        if (tenantId == 0) return true;
        var key = NormalizeDbKey(dbSlug);
        if (!_caches.TryGetValue(key, out var entry)) return false;
        return entry.RealIds.Contains(tenantId) || entry.DataIds.Contains(tenantId);
    }

    public static int ToDataTenantId(string? dbSlug, int realTenantId)
    {
        if (realTenantId == 0) return 0;
        var key = NormalizeDbKey(dbSlug);
        if (!_caches.TryGetValue(key, out var entry)) return 0;
        // If the id appears as a data-table id (i.e. a non-default tenant) it
        // passes through; otherwise it's the default tenant's real id (or
        // unknown) and collapses to 0.
        return entry.DataIds.Contains(realTenantId) ? realTenantId : 0;
    }
}
