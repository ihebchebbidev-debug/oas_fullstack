using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common.Data;
using MyApi.Modules.OAS.Common.Models;
using MyApi.Modules.OAS.ShopFloorAuth.Models;

namespace MyApi.Modules.OAS.Common;

/// <summary>
/// Separate EF Core context for the whole OAS module (spec §3). Never
/// references <c>MyApi.Data.ApplicationDbContext</c> or any socle entity —
/// enforced by the architecture test in OasIsolationTests (spec §3.4).
///
/// Every table is mapped to schema "public" (no HasDefaultSchema — spec §3)
/// with an explicit `oas_` prefix, uuid keys, and a tenant_id global query
/// filter mirroring the socle's ApplicationDbContext (spec §4.1). Referential
/// entities that implement <see cref="IOasSoftDeletable"/> also get an
/// IsDeleted=false filter; fact entities (declarations, events) never
/// implement it and are never soft-deleted.
///
/// No migrations. The schema is created by the operator running
/// public/OAS-SQL/001..004 by hand (spec §5.0) — OasDbContext only maps to
/// tables that must already exist.
/// </summary>
public class OasDbContext : DbContext
{
    private int _currentTenantId = 0;

    public OasDbContext(DbContextOptions<OasDbContext> options) : base(options) { }

    /// <summary>-1 = view all tenants within this OAS database (reserved for future admin tooling; not used in the initial phase).</summary>
    public void SetTenantId(int tenantId) => _currentTenantId = tenantId;
    public int GetTenantId() => _currentTenantId;

    public DbSet<OasPluginActivation> PluginActivations => Set<OasPluginActivation>();
    public DbSet<OasUser> Users => Set<OasUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Pick up every IEntityTypeConfiguration<T> in this assembly — each
        // OAS sub-module drops its own configuration class next to its
        // entity and it is wired up here automatically, without ever
        // touching this file again (spec §4.1: "une entité sans ToTable
        // explicite est un échec de revue" — enforced per-configuration,
        // not by a blanket convention here).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OasDbContext).Assembly,
            t => t.Namespace != null && t.Namespace.StartsWith("MyApi.Modules.OAS."));

        // IMPORTANT: EF Core's HasQueryFilter does NOT combine multiple calls
        // on the same entity — a second call silently REPLACES the first,
        // not ANDs it. Every entity gets exactly one HasQueryFilter call,
        // built from whichever marker interfaces it actually implements, so
        // a referential entity that is both tenant-scoped AND soft-deletable
        // never loses its tenant isolation to a soft-delete filter applied
        // afterwards (or vice versa).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var isTenantScoped = typeof(IOasTenantEntity).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(IOasSoftDeletable).IsAssignableFrom(clrType);

            if (!isTenantScoped && !isSoftDeletable) continue;

            var methodName = (isTenantScoped, isSoftDeletable) switch
            {
                (true, true) => nameof(SetTenantAndSoftDeleteFilter),
                (true, false) => nameof(SetTenantFilter),
                (false, true) => nameof(SetSoftDeleteFilter),
                _ => throw new InvalidOperationException("unreachable"),
            };

            typeof(OasDbContext)
                .GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(clrType)
                .Invoke(this, new object[] { modelBuilder });
        }
    }

    private void SetTenantFilter<T>(ModelBuilder modelBuilder) where T : class, IOasTenantEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e =>
            _currentTenantId == -1 || e.TenantId == _currentTenantId);
    }

    private void SetSoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : class, IOasSoftDeletable
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => e.IsDeleted == false);
    }

    private void SetTenantAndSoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : class, IOasTenantEntity, IOasSoftDeletable
    {
        modelBuilder.Entity<T>().HasQueryFilter(e =>
            (_currentTenantId == -1 || e.TenantId == _currentTenantId) && e.IsDeleted == false);
    }

    public override int SaveChanges()
    {
        StampAudit();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampAudit()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<OasEntityBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
                if (entry.Entity.TenantId == 0 && _currentTenantId > 0)
                {
                    entry.Entity.TenantId = _currentTenantId;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
