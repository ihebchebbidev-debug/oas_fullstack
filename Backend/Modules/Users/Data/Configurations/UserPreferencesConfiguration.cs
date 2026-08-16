using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Users.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Users.Data.Configurations
{
    public class UserPreferencesConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserPreferences>(entity =>
            {
                entity.ToTable("UserPreferences");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId).IsUnique();
                
                // PreferencesJson is JSONB column - matches actual database schema
                entity.Property(e => e.PreferencesJson)
                    .HasColumnType("jsonb")
                    .HasDefaultValueSql("'{}'::jsonb");
                
                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("NOW()");

                // FK to Users table (not MainAdminUsers) - matches actual database schema
                entity.HasOne(e => e.User)
                    .WithOne(u => u.Preferences)
                    .HasForeignKey<UserPreferences>(e => e.UserId)
                    .HasPrincipalKey<User>(u => u.Id)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
