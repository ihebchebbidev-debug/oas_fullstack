using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Installations.Models;

namespace MyApi.Modules.Installations.Data
{
    public class InstallationConfiguration : IEntityTypeConfiguration<Installation>
    {
        public void Configure(EntityTypeBuilder<Installation> builder)
        {
            builder.HasKey(i => i.Id);

            builder.Property(i => i.InstallationNumber)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(i => i.SiteAddress)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(i => i.InstallationType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(i => i.Status)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("active");

            builder.Property(i => i.Notes)
                .HasColumnType("text");

            builder.Property(i => i.CreatedBy)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(i => i.ModifiedBy)
                .HasMaxLength(100);

            builder.Property(i => i.Matricule)
                .HasMaxLength(100);

            // Indexes
            // Tenant-scoped unique (partial index enforced at the SQL level in
            // 20260725_Modules_Deep_Hardening.sql WHERE "IsDeleted"=false).
            // EF gets a non-unique composite index; the DB-level partial unique
            // index is the authoritative guard.
            builder.HasIndex(i => new { i.TenantId, i.InstallationNumber });

            builder.HasIndex(i => i.Matricule);
            builder.HasIndex(i => i.IsDeleted);

            builder.HasIndex(i => i.ContactId);
            builder.HasIndex(i => i.Status);

            // Relationships
            builder.HasMany(i => i.MaintenanceHistories)
                .WithOne(m => m.Installation)
                .HasForeignKey(m => m.InstallationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class MaintenanceHistoryConfiguration : IEntityTypeConfiguration<MaintenanceHistory>
    {
        public void Configure(EntityTypeBuilder<MaintenanceHistory> builder)
        {
            builder.HasKey(m => m.Id);

            builder.Property(m => m.MaintenanceType)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(m => m.Description)
                .HasColumnType("text");

            builder.Property(m => m.PerformedBy)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(m => m.Cost)
                .HasColumnType("decimal(18,2)");

            // Indexes
            builder.HasIndex(m => m.InstallationId);
            builder.HasIndex(m => m.MaintenanceDate);
            builder.HasIndex(m => m.MaintenanceType);
        }
    }
}
