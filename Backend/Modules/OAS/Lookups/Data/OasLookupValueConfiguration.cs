using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Lookups.Models;

namespace MyApi.Modules.OAS.Lookups.Data;

public class OasLookupValueConfiguration : IEntityTypeConfiguration<OasLookupValue>
{
    public void Configure(EntityTypeBuilder<OasLookupValue> b)
    {
        b.ToTable("oas_lookup_values");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Type).HasColumnName("type").IsRequired();
        b.Property(x => x.Code).HasColumnName("code").IsRequired();
        b.Property(x => x.Label).HasColumnName("label").IsRequired();
        b.Property(x => x.Color).HasColumnName("color");
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Property(x => x.IsDefault).HasColumnName("is_default");
        b.Property(x => x.ArchivedAt).HasColumnName("archived_at");
        b.Ignore(x => x.CreatedAt);
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TenantId, x.Type, x.Code }).IsUnique();
    }
}
