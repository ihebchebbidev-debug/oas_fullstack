namespace MyApi.Infrastructure;

/// <summary>
/// Interface for entities that are tenant-scoped.
/// EF Core Global Query Filters automatically append WHERE "TenantId" = X.
/// SaveChangesAsync auto-stamps TenantId on new entities.
/// 
/// All entity models EXCEPT MainAdminUser and Tenant should implement this.
/// </summary>
public interface ITenantEntity
{
    int TenantId { get; set; }
}
