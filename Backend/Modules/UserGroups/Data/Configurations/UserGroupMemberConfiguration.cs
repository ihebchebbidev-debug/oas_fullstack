using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Shared.Data.Configurations;
using MyApi.Modules.UserGroups.Models;

namespace MyApi.Modules.UserGroups.Data.Configurations
{
    public class UserGroupMemberConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserGroupMember>(entity =>
            {
                entity.ToTable("UserGroupMembers");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.Property(e => e.AssignedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                entity.HasOne(m => m.Group)
                    .WithMany(g => g.Members)
                    .HasForeignKey(m => m.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
