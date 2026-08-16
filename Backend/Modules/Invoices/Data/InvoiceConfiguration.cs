using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Invoices.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Invoices.Data
{
    public class InvoiceConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Invoice>(e =>
            {
                e.ToTable("Invoices");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.TenantId, x.InvoiceNumber }).IsUnique()
                    .HasFilter("\"InvoiceNumber\" IS NOT NULL");
                e.HasIndex(x => new { x.TenantId, x.ContactId });
                e.HasIndex(x => new { x.TenantId, x.SaleId });
                e.HasIndex(x => new { x.TenantId, x.ServiceOrderId });
                e.HasIndex(x => new { x.TenantId, x.Status });
                e.HasMany(x => x.Lines)
                    .WithOne(l => l.Invoice!)
                    .HasForeignKey(l => l.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }

    public class InvoiceLineConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InvoiceLine>(e =>
            {
                e.ToTable("InvoiceLines");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.TenantId, x.InvoiceId });
                e.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId });
            });
        }
    }

    public class InvoiceActivityConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InvoiceActivity>(e =>
            {
                e.ToTable("InvoiceActivities");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.TenantId, x.InvoiceId, x.CreatedAt });
                e.HasIndex(x => new { x.TenantId, x.Type });
                e.HasOne(x => x.Invoice)
                    .WithMany()
                    .HasForeignKey(x => x.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}