using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Users.Models;
using MyApi.Modules.Roles.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Roles.Data.Configurations
{
    public class UserRoleConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("UserRoles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.RoleId });
                entity.Property(e => e.AssignedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                // Define relationships - properly reference User.UserRoles collection
                entity.HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
