using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Cadences.Models;

namespace MyApi.Modules.OAS.Cadences.Data;

public class OasRoutingConfiguration : IEntityTypeConfiguration<OasRouting>
{
    public void Configure(EntityTypeBuilder<OasRouting> b)
    {
        b.ToTable("oas_routings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.Rate).HasColumnName("rate").HasColumnType("numeric(10,2)");
        b.Property(x => x.CycleTimeSec).HasColumnName("cycle_time_sec").HasColumnType("numeric(10,3)");
        b.Property(x => x.ChangeoverTargetMin).HasColumnName("changeover_target_min");
        b.Property(x => x.OperatorsRequired).HasColumnName("operators_required");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.ProductId, x.PostId }).IsUnique();
    }
}

public class OasRoutingVersionConfiguration : IEntityTypeConfiguration<OasRoutingVersion>
{
    public void Configure(EntityTypeBuilder<OasRoutingVersion> b)
    {
        b.ToTable("oas_routing_versions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.Rate).HasColumnName("rate").HasColumnType("numeric(10,2)");
        b.Property(x => x.Version).HasColumnName("version");
        b.Property(x => x.Since).HasColumnName("since");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TenantId, x.ProductId, x.PostId, x.Version }).IsUnique();
    }
}
