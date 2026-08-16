using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Auth.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Auth.Data.Configurations
{
    public class MainAdminUserConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MainAdminUser>(entity =>
            {
                entity.ToTable("MainAdminUsers");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.IsActive);
                
                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive)
                    .HasDefaultValue(true);
                entity.Property(e => e.OnboardingCompleted)
                    .HasDefaultValue(false);
                entity.Property(e => e.CompanyLogoUrl)
                    .HasColumnName("CompanyLogoUrl")
                    .HasMaxLength(500);
                entity.Property(e => e.ProfilePictureUrl)
                    .HasColumnName("ProfilePictureUrl")
                    .HasMaxLength(500);
            });
        }
    }
}
