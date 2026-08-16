using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Sales.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Sales.Data
{
    public class SaleConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.ToTable("Sales");
                entity.HasKey(e => e.Id);

                entity.HasMany(e => e.Items)
                    .WithOne(i => i.Sale)
                    .HasForeignKey(i => i.SaleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Activities)
                    .WithOne(a => a.Sale)
                    .HasForeignKey(a => a.SaleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }

    public class SaleItemConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SaleItem>(entity =>
            {
                entity.ToTable("SaleItems");
                entity.HasKey(e => e.Id);
            });
        }
    }

    public class SaleActivityConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SaleActivity>(entity =>
            {
                entity.ToTable("SaleActivities");
                entity.HasKey(e => e.Id);
            });
        }
    }
}
