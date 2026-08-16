using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Shared.Models;

namespace MyApi.Modules.Shared.Data.Configurations
{
    /// <summary>
    /// Configuration for EntityFormDocument entity
    /// Ensures Status is stored as string (lowercase) to match DB check constraint
    /// </summary>
    public class EntityFormDocumentConfiguration : IEntityTypeConfiguration<EntityFormDocument>
    {
        public void Configure(EntityTypeBuilder<EntityFormDocument> builder)
        {
            builder.ToTable("EntityFormDocuments");

            builder.HasKey(e => e.Id);

            // Store Status enum as lowercase string to match DB check constraint
            builder.Property(e => e.Status)
                .HasConversion(
                    v => v.ToString().ToLower(),        // Convert enum to lowercase string when saving
                    v => ParseStatus(v))                 // Parse string back to enum when reading
                .HasMaxLength(20)
                .IsRequired();

            // EntityType is already string
            builder.Property(e => e.EntityType)
                .HasMaxLength(50)
                .IsRequired();

            // Responses as JSONB
            builder.Property(e => e.Responses)
                .HasColumnType("jsonb")
                .IsRequired();

            // Form relationship
            builder.HasOne(e => e.Form)
                .WithMany()
                .HasForeignKey(e => e.FormId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(e => new { e.EntityType, e.EntityId });
            builder.HasIndex(e => e.FormId);
            builder.HasIndex(e => e.IsDeleted);
        }

        private static FormDocumentStatus ParseStatus(string value)
        {
            if (string.IsNullOrEmpty(value))
                return FormDocumentStatus.Draft;

            return value.ToLower() switch
            {
                "draft" => FormDocumentStatus.Draft,
                "completed" => FormDocumentStatus.Completed,
                _ => FormDocumentStatus.Draft
            };
        }
    }
}
