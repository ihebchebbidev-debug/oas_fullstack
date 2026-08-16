using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Dispatches.Models;

namespace MyApi.Modules.Dispatches.Data
{
    public class DispatchTechnicianConfiguration : IEntityTypeConfiguration<DispatchTechnician>
    {
        public void Configure(EntityTypeBuilder<DispatchTechnician> builder)
        {
            builder.ToTable("DispatchTechnicians");
            builder.HasKey(dt => dt.Id);
            builder.Property(dt => dt.DispatchId).IsRequired();
            builder.Property(dt => dt.TechnicianId).IsRequired();
            builder.Property(dt => dt.Role).HasMaxLength(50);
        }
    }
}
