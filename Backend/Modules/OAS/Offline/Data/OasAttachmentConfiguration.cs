using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Offline.Models;

namespace MyApi.Modules.OAS.Offline.Data;

public class OasAttachmentConfiguration : IEntityTypeConfiguration<OasAttachment>
{
    public void Configure(EntityTypeBuilder<OasAttachment> b)
    {
        b.ToTable("oas_attachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Bucket).HasColumnName("bucket");
        b.Property(x => x.Path).HasColumnName("path").IsRequired();
        b.Property(x => x.MimeType).HasColumnName("mime_type");
        b.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        b.Property(x => x.EntityTable).HasColumnName("entity_table");
        b.Property(x => x.EntityId).HasColumnName("entity_id");
        b.Property(x => x.UploadedBy).HasColumnName("uploaded_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.TenantId, x.EntityTable, x.EntityId });
    }
}
