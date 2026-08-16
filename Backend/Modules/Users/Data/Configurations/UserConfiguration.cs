using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Users.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Users.Data.Configurations
{
    public class UserConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.CreatedDate);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.IsDeleted);
                
                entity.Property(e => e.CreatedDate)
                    .HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive)
                    .HasDefaultValue(true);
                entity.Property(e => e.IsDeleted)
                    .HasDefaultValue(false);
            });
        }
    }
}
