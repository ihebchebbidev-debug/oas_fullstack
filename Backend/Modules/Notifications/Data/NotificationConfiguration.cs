using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Notifications.Models;

namespace MyApi.Modules.Notifications.Data
{
    public class NotificationConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("Id");

                entity.Property(e => e.UserId)
                    .IsRequired()
                    .HasColumnName("UserId");

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnName("Title");

                entity.Property(e => e.Description)
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnName("Description");

                entity.Property(e => e.Type)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue("info")
                    .HasColumnName("Type");

                entity.Property(e => e.Category)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue("system")
                    .HasColumnName("Category");

                entity.Property(e => e.Link)
                    .HasMaxLength(255)
                    .HasColumnName("Link");

                entity.Property(e => e.RelatedEntityId)
                    .HasColumnName("RelatedEntityId");

                entity.Property(e => e.RelatedEntityType)
                    .HasMaxLength(50)
                    .HasColumnName("RelatedEntityType");

                entity.Property(e => e.IsRead)
                    .IsRequired()
                    .HasDefaultValue(false)
                    .HasColumnName("IsRead");

                entity.Property(e => e.ReadAt)
                    .HasColumnName("ReadAt");

                entity.Property(e => e.CreatedAt)
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .HasColumnName("CreatedAt");

                // Indexes for performance
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.IsRead);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.UserId, e.IsRead });
            });
        }
    }
}
