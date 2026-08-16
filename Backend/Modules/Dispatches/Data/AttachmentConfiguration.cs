using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Dispatches.Models;

namespace MyApi.Modules.Dispatches.Data
{
    public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> builder)
        {
            builder.ToTable("Attachments");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.DispatchId).IsRequired();
            builder.Property(a => a.FileName).IsRequired().HasMaxLength(255);
            builder.Property(a => a.FilePath).IsRequired().HasMaxLength(500);
            builder.Property(a => a.FileSize).IsRequired();
            builder.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
            builder.Property(a => a.Category).HasColumnName("AttachmentType").HasMaxLength(50).IsRequired();
            builder.Property(a => a.UploadedBy).HasMaxLength(100).IsRequired();
        }
    }
}
