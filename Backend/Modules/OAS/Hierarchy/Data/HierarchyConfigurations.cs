using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Hierarchy.Models;

namespace MyApi.Modules.OAS.Hierarchy.Data;

public class OasSiteConfiguration : IEntityTypeConfiguration<OasSite>
{
    public void Configure(EntityTypeBuilder<OasSite> b)
    {
        b.ToTable("oas_sites");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Code).HasColumnName("code").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Timezone).HasColumnName("timezone");
        b.Property(x => x.Address).HasColumnName("address");
        b.Property(x => x.ArchivedAt).HasColumnName("archived_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class OasZoneConfiguration : IEntityTypeConfiguration<OasZone>
{
    public void Configure(EntityTypeBuilder<OasZone> b)
    {
        b.ToTable("oas_zones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.SiteId).HasColumnName("site_id");
        b.Property(x => x.Code).HasColumnName("code").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Property(x => x.ArchivedAt).HasColumnName("archived_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasIndex(x => x.SiteId);
    }
}

public class OasLineConfiguration : IEntityTypeConfiguration<OasLine>
{
    public void Configure(EntityTypeBuilder<OasLine> b)
    {
        b.ToTable("oas_lines");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ZoneId).HasColumnName("zone_id");
        b.Property(x => x.Code).HasColumnName("code").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Property(x => x.TargetOee).HasColumnName("target_oee").HasColumnType("numeric(5,2)");
        b.Property(x => x.ArchivedAt).HasColumnName("archived_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasIndex(x => x.ZoneId);
    }
}

public class OasPostConfiguration : IEntityTypeConfiguration<OasPost>
{
    public void Configure(EntityTypeBuilder<OasPost> b)
    {
        b.ToTable("oas_posts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.LineId).HasColumnName("line_id");
        b.Property(x => x.Code).HasColumnName("code").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.QrToken).HasColumnName("qr_token").IsRequired();
        b.Property(x => x.QrRotatedAt).HasColumnName("qr_rotated_at");
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Property(x => x.PostType).HasColumnName("post_type");
        b.Property(x => x.IsCritical).HasColumnName("is_critical");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.ArchivedAt).HasColumnName("archived_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasIndex(x => x.QrToken).IsUnique();
        b.HasIndex(x => x.LineId);
    }
}

public class OasPostLayoutConfiguration : IEntityTypeConfiguration<OasPostLayout>
{
    public void Configure(EntityTypeBuilder<OasPostLayout> b)
    {
        b.ToTable("oas_post_layouts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.LayoutKey).HasColumnName("layout_key");
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Property(x => x.ColSpan).HasColumnName("col_span");
        b.Property(x => x.RowSpan).HasColumnName("row_span");
        b.Property(x => x.X).HasColumnName("x").HasColumnType("numeric(10,2)");
        b.Property(x => x.Y).HasColumnName("y").HasColumnType("numeric(10,2)");
        b.Ignore(x => x.CreatedAt);
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TenantId, x.PostId, x.LayoutKey }).IsUnique();
    }
}
