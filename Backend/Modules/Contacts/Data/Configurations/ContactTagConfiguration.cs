using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Contacts.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Contacts.Data.Configurations
{
    public class ContactTagConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ContactTag>(entity =>
            {
                entity.ToTable("ContactTags");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("NOW()");
                entity.Property(e => e.Color).HasDefaultValue("#3b82f6");
            });

            modelBuilder.Entity<ContactTagAssignment>(entity =>
            {
                entity.ToTable("ContactTagAssignments");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ContactId);
                entity.HasIndex(e => e.TagId);
                entity.HasIndex(e => new { e.ContactId, e.TagId }).IsUnique();
                entity.Property(e => e.AssignedDate).HasDefaultValueSql("NOW()");

                entity.HasOne(ta => ta.Contact)
                    .WithMany(c => c.TagAssignments)
                    .HasForeignKey(ta => ta.ContactId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ta => ta.Tag)
                    .WithMany(t => t.ContactAssignments)
                    .HasForeignKey(ta => ta.TagId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
