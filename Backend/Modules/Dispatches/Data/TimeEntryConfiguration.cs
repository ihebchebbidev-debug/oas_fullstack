using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Dispatches.Models;

namespace MyApi.Modules.Dispatches.Data
{
    public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
    {
        public void Configure(EntityTypeBuilder<TimeEntry> builder)
        {
            builder.ToTable("TimeEntries");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.DispatchId).IsRequired();
            builder.Property(t => t.TechnicianId).IsRequired();
            builder.Property(t => t.StartTime).IsRequired();
            builder.Property(t => t.EndTime);
            // Store duration in minutes. Use larger precision to avoid overflow.
            builder.Property(t => t.Duration).HasColumnType("decimal(18,2)");
            builder.Property(t => t.WorkType).HasColumnName("ActivityType").HasMaxLength(50).IsRequired();
            builder.Property(t => t.Description);
            builder.Property(t => t.Billable).HasDefaultValue(true);
            builder.Property(t => t.CreatedDate).IsRequired();
            builder.Property(t => t.OverrunFlag).HasDefaultValue(false);
            builder.Property(t => t.OverrunReason).HasMaxLength(500);
        }
    }
}
