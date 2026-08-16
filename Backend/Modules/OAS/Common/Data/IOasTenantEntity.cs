namespace MyApi.Modules.OAS.Common.Data;

/// <summary>
/// Marks an OAS entity as scoped by the socle's row-level company id
/// (X-Target-Tenant). Mirrors <c>MyApi.Infrastructure.ITenantEntity</c> but
/// is intentionally a separate interface — OAS never references socle types
/// (spec §3.2: no navigation from an OAS entity to a socle entity).
/// </summary>
public interface IOasTenantEntity
{
    int TenantId { get; set; }
}

/// <summary>
/// Marks a referential OAS entity as soft-deletable. Per spec §4.1, this
/// applies to referentials only — facts (declarations, events) are never
/// deleted and must NOT implement this interface.
/// </summary>
public interface IOasSoftDeletable
{
    bool IsDeleted { get; set; }
}

/// <summary>
/// Common columns every OAS table carries: uuid PK, row-level tenant scope,
/// and audit timestamps (spec §3, §4.1).
/// </summary>
public abstract class OasEntityBase : IOasTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
