using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Dispatches.Models;

namespace MyApi.Modules.Dispatches.Data
{
    public class DispatchAuditLogConfiguration : IEntityTypeConfiguration<DispatchAuditLog>
    {
        public void Configure(EntityTypeBuilder<DispatchAuditLog> builder)
        {
            builder.ToTable("DispatchAuditLogs");
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => e.DispatchId);
            builder.HasIndex(e => new { e.EventType, e.CreatedAt });
            builder.Property(e => e.EventType).HasMaxLength(60).IsRequired();
            builder.Property(e => e.DispatchNumber).HasMaxLength(100);
            builder.Property(e => e.OldStatus).HasMaxLength(60);
            builder.Property(e => e.NewStatus).HasMaxLength(60);
            builder.Property(e => e.Reason).HasMaxLength(1000);
            builder.Property(e => e.SaleId).HasMaxLength(100);
            builder.Property(e => e.OfferId).HasMaxLength(100);
            builder.Property(e => e.ActorUserId).HasMaxLength(100);
            builder.Property(e => e.ActorName).HasMaxLength(200);
        }
    }
}