using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Deals.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Deals.Data
{
    public class DealConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Deal>(entity =>
            {
                entity.ToTable("Deals");
                entity.HasKey(d => d.Id);

                entity.HasMany(d => d.Items)
                    .WithOne(i => i.Deal)
                    .HasForeignKey(i => i.DealId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(d => d.Activities)
                    .WithOne(a => a.Deal)
                    .HasForeignKey(a => a.DealId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(d => d.ContactId);
                entity.HasIndex(d => d.ProjectId);
                entity.HasIndex(d => d.Stage);
            });
        }
    }

    public class DealItemConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DealItem>(entity =>
            {
                entity.ToTable("DealItems");
                entity.HasKey(i => i.Id);
            });
        }
    }

    public class DealActivityConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DealActivity>(entity =>
            {
                entity.ToTable("DealActivities");
                entity.HasKey(a => a.Id);
            });
        }
    }
}
