using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Contacts.Models;
using MyApi.Modules.Shared.Data.Configurations;
using MyApi.Modules.UserGroups.Models;

namespace MyApi.Modules.Contacts.Data.Configurations
{
    public class ContactUserGroupConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ContactUserGroupAssignment>(entity =>
            {
                entity.ToTable("ContactUserGroups");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ContactId);
                entity.HasIndex(e => e.UserGroupId);
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => new { e.ContactId, e.UserGroupId }).IsUnique();
                entity.Property(e => e.AssignedAt).HasDefaultValueSql("NOW()");

                entity.HasOne(a => a.Contact)
                    .WithMany(c => c.UserGroupAssignments)
                    .HasForeignKey(a => a.ContactId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.UserGroup)
                    .WithMany()
                    .HasForeignKey(a => a.UserGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
