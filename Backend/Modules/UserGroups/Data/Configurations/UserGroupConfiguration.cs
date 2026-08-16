using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Shared.Data.Configurations;
using MyApi.Modules.UserGroups.Models;

namespace MyApi.Modules.UserGroups.Data.Configurations
{
    public class UserGroupConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserGroup>(entity =>
            {
                entity.ToTable("UserGroups");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Name, e.TenantId }).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            });
        }
    }
}
