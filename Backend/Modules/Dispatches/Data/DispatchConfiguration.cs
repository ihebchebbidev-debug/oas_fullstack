using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Dispatches.Models;

namespace MyApi.Modules.Dispatches.Data
{
    public class DispatchConfiguration : IEntityTypeConfiguration<Dispatch>
    {
        public void Configure(EntityTypeBuilder<Dispatch> builder)
        {
            builder.ToTable("Dispatches");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.WorkLocationJson).HasColumnType("jsonb");
            builder.Property(d => d.IsDeleted).HasDefaultValue(false);

            // Configure one-to-many relationships (no navigation property on child side)
            builder.HasMany(d => d.TimeEntries)
                .WithOne()
                .HasForeignKey(t => t.DispatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(d => d.Expenses)
                .WithOne()
                .HasForeignKey(e => e.DispatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(d => d.MaterialsUsed)
                .WithOne()
                .HasForeignKey(m => m.DispatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(d => d.Attachments)
                .WithOne()
                .HasForeignKey(a => a.DispatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(d => d.Notes)
                .WithOne()
                .HasForeignKey(n => n.DispatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(d => d.AssignedTechnicians)
                .WithOne()
                .HasForeignKey(dt => dt.DispatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(d => d.DispatchJobs)
                .WithOne()
                .HasForeignKey(dj => dj.DispatchId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
