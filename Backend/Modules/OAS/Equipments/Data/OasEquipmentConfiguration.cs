using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Equipments.Models;

namespace MyApi.Modules.OAS.Equipments.Data;

public class OasEquipmentConfiguration : IEntityTypeConfiguration<OasEquipment>
{
    public void Configure(EntityTypeBuilder<OasEquipment> b)
    {
        b.ToTable("oas_equipments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.Code).HasColumnName("code").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.SerialNumber).HasColumnName("serial_number");
        b.Property(x => x.Manufacturer).HasColumnName("manufacturer");
        b.Property(x => x.CommissionedAt).HasColumnName("commissioned_at");
        b.Property(x => x.Criticality).HasColumnName("criticality"); // native enum, see OasNpgsqlEnums
        b.Property(x => x.ArchivedAt).HasColumnName("archived_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasIndex(x => x.PostId);
    }
}
