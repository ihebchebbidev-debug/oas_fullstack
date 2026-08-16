using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Quality.Models;

namespace MyApi.Modules.OAS.Quality.Data;

public class OasQualityCheckConfiguration : IEntityTypeConfiguration<OasQualityCheck>
{
    public void Configure(EntityTypeBuilder<OasQualityCheck> b)
    {
        b.ToTable("oas_quality_checks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ClientEventId).HasColumnName("client_event_id");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.ProductionOrderId).HasColumnName("production_order_id");
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.Property(x => x.ChangeoverId).HasColumnName("changeover_id");
        b.Property(x => x.EventId).HasColumnName("event_id");
        b.Property(x => x.TemplateId).HasColumnName("template_id");
        b.Property(x => x.CheckType).HasColumnName("check_type");
        b.Property(x => x.Result).HasColumnName("result");
        b.Property(x => x.QuantityChecked).HasColumnName("quantity_checked").HasColumnType("numeric(12,2)");
        b.Property(x => x.QuantityRejected).HasColumnName("quantity_rejected").HasColumnType("numeric(12,2)");
        b.Property(x => x.CauseId).HasColumnName("cause_id");
        b.Property(x => x.PhotoPath).HasColumnName("photo_path");
        b.Property(x => x.Note).HasColumnName("note");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        b.Property(x => x.InspectorId).HasColumnName("inspector_id");
        b.Property(x => x.CreatedAt).HasColumnName("received_at");
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => x.ClientEventId).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.PostId, x.OccurredAt });
    }
}

public class OasQualityCheckTemplateConfiguration : IEntityTypeConfiguration<OasQualityCheckTemplate>
{
    public void Configure(EntityTypeBuilder<OasQualityCheckTemplate> b)
    {
        b.ToTable("oas_quality_check_templates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Code).HasColumnName("code").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.CheckType).HasColumnName("check_type");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class OasQualityCheckTemplateItemConfiguration : IEntityTypeConfiguration<OasQualityCheckTemplateItem>
{
    public void Configure(EntityTypeBuilder<OasQualityCheckTemplateItem> b)
    {
        b.ToTable("oas_quality_check_template_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.TemplateId).HasColumnName("template_id");
        b.Property(x => x.Label).HasColumnName("label").IsRequired();
        b.Property(x => x.ValueType).HasColumnName("value_type");
        b.Property(x => x.MinValue).HasColumnName("min_value").HasColumnType("numeric(12,4)");
        b.Property(x => x.MaxValue).HasColumnName("max_value").HasColumnType("numeric(12,4)");
        b.Property(x => x.IsRequired).HasColumnName("is_required");
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Ignore(x => x.CreatedAt);
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TemplateId, x.SortOrder });
    }
}
