using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Offers.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Offers.Data
{
    public class OfferConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Offer>(entity =>
            {
                entity.ToTable("Offers");
                entity.HasKey(o => o.Id);

                entity.HasMany(o => o.Items)
                    .WithOne(i => i.Offer)
                    .HasForeignKey(i => i.OfferId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(o => o.Activities)
                    .WithOne(a => a.Offer)
                    .HasForeignKey(a => a.OfferId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }

    public class OfferItemConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OfferItem>(entity =>
            {
                entity.ToTable("OfferItems");
                entity.HasKey(i => i.Id);
            });
        }
    }

    public class OfferActivityConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OfferActivity>(entity =>
            {
                entity.ToTable("OfferActivities");
                entity.HasKey(a => a.Id);
            });
        }
    }
}
