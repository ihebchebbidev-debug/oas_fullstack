using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Roles.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Roles.Data.Configurations
{
    public class RoleConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Name, e.TenantId }).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });
        }
    }
}
