using Microsoft.EntityFrameworkCore;
using MyApi.Modules.RetenueSource.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.RetenueSource.Data
{
    public class RSRecordConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RSRecord>(entity =>
            {
                entity.ToTable("RSRecords");
                entity.HasKey(r => r.Id);

                entity.Property(r => r.EntityType).IsRequired().HasMaxLength(20);
                entity.Property(r => r.EntityId).IsRequired();
                entity.Property(r => r.InvoiceNumber).IsRequired().HasMaxLength(100);
                entity.Property(r => r.InvoiceAmount).HasColumnType("decimal(15,2)");
                entity.Property(r => r.AmountPaid).HasColumnType("decimal(15,2)");
                entity.Property(r => r.RSAmount).HasColumnType("decimal(15,2)");
                entity.Property(r => r.RSTypeCode).IsRequired().HasMaxLength(10);
                entity.Property(r => r.SupplierName).IsRequired().HasMaxLength(255);
                entity.Property(r => r.SupplierTaxId).IsRequired().HasMaxLength(50);
                entity.Property(r => r.PayerName).IsRequired().HasMaxLength(255);
                entity.Property(r => r.PayerTaxId).IsRequired().HasMaxLength(50);
                entity.Property(r => r.Status).IsRequired().HasMaxLength(20).HasDefaultValue("pending");
                entity.Property(r => r.TEJExported).HasDefaultValue(false);

                // Index for efficient monthly queries
                entity.HasIndex(r => new { r.PaymentDate, r.Status });
                // Index for entity lookup
                entity.HasIndex(r => new { r.EntityType, r.EntityId });
                // Unique constraint to prevent duplicates
                entity.HasIndex(r => new { r.InvoiceNumber, r.PaymentDate, r.EntityId, r.EntityType }).IsUnique();
            });
        }
    }

    public class TEJExportLogConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TEJExportLog>(entity =>
            {
                entity.ToTable("TEJExportLogs");
                entity.HasKey(l => l.Id);

                entity.Property(l => l.FileName).IsRequired().HasMaxLength(255);
                entity.Property(l => l.ExportedBy).IsRequired().HasMaxLength(100);
                entity.Property(l => l.TotalRSAmount).HasColumnType("decimal(15,2)");
                entity.Property(l => l.Status).IsRequired().HasMaxLength(20);

                entity.HasIndex(l => new { l.Year, l.Month });
            });
        }
    }
}
