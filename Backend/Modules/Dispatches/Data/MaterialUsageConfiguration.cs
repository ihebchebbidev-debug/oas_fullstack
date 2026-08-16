using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Dispatches.Models;

namespace MyApi.Modules.Dispatches.Data
{
    public class MaterialUsageConfiguration : IEntityTypeConfiguration<MaterialUsage>
    {
        public void Configure(EntityTypeBuilder<MaterialUsage> builder)
        {
            builder.ToTable("MaterialUsage");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.DispatchId).IsRequired();
            builder.Property(m => m.ArticleId);
            builder.Property(m => m.Description).HasMaxLength(500).IsRequired();
            builder.Property(m => m.Quantity).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(m => m.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(m => m.TotalPrice).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(m => m.RecordedBy).HasMaxLength(100).IsRequired();
            builder.Property(m => m.Unit).HasMaxLength(20).IsRequired();
            builder.Property(m => m.OverrunFlag).HasDefaultValue(false);
            builder.Property(m => m.OverrunReason).HasMaxLength(500);
            builder.Property(m => m.ApprovalStatus).HasMaxLength(20).HasDefaultValue("pending");
            builder.Property(m => m.ApprovedBy).HasMaxLength(100);
            builder.Property(m => m.ApprovedAt);
            builder.Property(m => m.RejectionReason).HasMaxLength(500);
        }
    }
}
