using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Products.Models;

namespace MyApi.Modules.OAS.Products.Data;

public class OasProductConfiguration : IEntityTypeConfiguration<OasProduct>
{
    public void Configure(EntityTypeBuilder<OasProduct> b)
    {
        b.ToTable("oas_products");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Reference).HasColumnName("reference").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Customer).HasColumnName("customer");
        b.Property(x => x.Unit).HasColumnName("unit");
        b.Property(x => x.ArchivedAt).HasColumnName("archived_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        b.HasIndex(x => new { x.TenantId, x.Reference }).IsUnique();
    }
}

public class OasProductionOrderConfiguration : IEntityTypeConfiguration<OasProductionOrder>
{
    public void Configure(EntityTypeBuilder<OasProductionOrder> b)
    {
        b.ToTable("oas_production_orders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.OrderNumber).HasColumnName("order_number").IsRequired();
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.Property(x => x.LineId).HasColumnName("line_id");
        b.Property(x => x.QuantityPlanned).HasColumnName("quantity_planned").HasColumnType("numeric(12,2)");
        b.Property(x => x.QuantityProduced).HasColumnName("quantity_produced").HasColumnType("numeric(12,2)");
        b.Property(x => x.QuantityScrapped).HasColumnName("quantity_scrapped").HasColumnType("numeric(12,2)");
        b.Property(x => x.DueDate).HasColumnName("due_date");
        b.Property(x => x.Status).HasColumnName("status"); // native enum
        b.Property(x => x.Priority).HasColumnName("priority");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.OrderNumber }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.LineId, x.Status });
    }
}
