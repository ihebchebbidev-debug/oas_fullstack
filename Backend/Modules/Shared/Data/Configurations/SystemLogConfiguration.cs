using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Shared.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Shared.Data.Configurations
{
    public class SystemLogConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SystemLog>(entity =>
            {
                entity.ToTable("SystemLogs");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Timestamp)
                    .IsRequired()
                    .HasDefaultValueSql("NOW()");

                entity.Property(e => e.Level)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.Message)
                    .IsRequired();

                entity.Property(e => e.Module)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Action)
                    .HasMaxLength(50)
                    .HasDefaultValue("other");

                entity.Property(e => e.UserId)
                    .HasMaxLength(100);

                entity.Property(e => e.UserName)
                    .HasMaxLength(200);

                entity.Property(e => e.EntityType)
                    .HasMaxLength(100);

                entity.Property(e => e.EntityId)
                    .HasMaxLength(100);

                entity.Property(e => e.IpAddress)
                    .HasMaxLength(45);

                entity.Property(e => e.Metadata)
                    .HasColumnType("jsonb");

                // Indexes for efficient querying
                entity.HasIndex(e => e.Timestamp)
                    .IsDescending();

                entity.HasIndex(e => e.Level);

                entity.HasIndex(e => e.Module);

                entity.HasIndex(e => e.UserId);

                entity.HasIndex(e => e.Action);

                entity.HasIndex(e => new { e.Level, e.Module, e.Timestamp });
            });
        }
    }
}
