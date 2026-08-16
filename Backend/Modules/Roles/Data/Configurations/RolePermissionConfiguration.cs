using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Roles.Models;

namespace MyApi.Modules.Roles.Data.Configurations
{
    public class RolePermissionConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.ToTable("RolePermissions");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.RoleId)
                    .IsRequired();

                entity.Property(e => e.Module)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Action)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Granted)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                    .IsRequired()
                    .HasDefaultValueSql("NOW()");

                entity.Property(e => e.UpdatedAt);

                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(100)
                    .HasDefaultValue("system");

                entity.Property(e => e.ModifiedBy)
                    .HasMaxLength(100);

                // Unique constraint on RoleId + Module + Action
                entity.HasIndex(e => new { e.RoleId, e.Module, e.Action })
                    .IsUnique()
                    .HasDatabaseName("UQ_RolePermissions_Role_Module_Action");

                // Index for faster lookups
                entity.HasIndex(e => e.RoleId)
                    .HasDatabaseName("IX_RolePermissions_RoleId");

                entity.HasIndex(e => new { e.Module, e.Action })
                    .HasDatabaseName("IX_RolePermissions_Module_Action");

                // Foreign key to Role
                entity.HasOne(e => e.Role)
                    .WithMany()
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
