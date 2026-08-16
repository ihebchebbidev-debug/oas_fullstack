using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.ServiceOrders.Models;

namespace MyApi.Modules.ServiceOrders.Data
{
    public class ServiceOrderConfiguration : IEntityTypeConfiguration<ServiceOrder>
    {
        public void Configure(EntityTypeBuilder<ServiceOrder> builder)
        {
            builder.ToTable("ServiceOrders");
            builder.HasKey(s => s.Id);

            // Navigation - Jobs
            builder.HasMany(s => s.Jobs)
                .WithOne(j => j.ServiceOrder)
                .HasForeignKey(j => j.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Navigation - Materials (explicitly configure to prevent shadow FK)
            builder.HasMany(s => s.Materials)
                .WithOne(m => m.ServiceOrder)
                .HasForeignKey(m => m.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class ServiceOrderJobConfiguration : IEntityTypeConfiguration<ServiceOrderJob>
    {
        public void Configure(EntityTypeBuilder<ServiceOrderJob> builder)
        {
            builder.ToTable("ServiceOrderJobs");
            builder.HasKey(j => j.Id);
        }
    }

    public class ServiceOrderMaterialConfiguration : IEntityTypeConfiguration<ServiceOrderMaterial>
    {
        public void Configure(EntityTypeBuilder<ServiceOrderMaterial> builder)
        {
            builder.ToTable("ServiceOrderMaterials");
            builder.HasKey(m => m.Id);

            // Relationship is already configured in ServiceOrderConfiguration
            // Just ensure column mappings are correct
            builder.Property(m => m.ServiceOrderId).HasColumnName("ServiceOrderId");
            builder.Property(m => m.InternalComment).HasColumnName("InternalComment").HasMaxLength(1000);
            builder.Property(m => m.ExternalComment).HasColumnName("ExternalComment").HasMaxLength(1000);
            builder.Property(m => m.Replacing).HasColumnName("Replacing");
            builder.Property(m => m.OldArticleModel).HasColumnName("OldArticleModel").HasMaxLength(255);
            builder.Property(m => m.OldArticleStatus).HasColumnName("OldArticleStatus").HasMaxLength(50);
            builder.Property(m => m.EstimatedQuantity)
                .HasColumnName("EstimatedQuantity")
                .HasColumnType("decimal(18,2)");
        }
    }

    public class ServiceOrderTimeEntryConfiguration : IEntityTypeConfiguration<ServiceOrderTimeEntry>
    {
        public void Configure(EntityTypeBuilder<ServiceOrderTimeEntry> builder)
        {
            builder.ToTable("ServiceOrderTimeEntries");
            builder.HasKey(t => t.Id);

            builder.HasOne(t => t.ServiceOrder)
                .WithMany()
                .HasForeignKey(t => t.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class ServiceOrderExpenseConfiguration : IEntityTypeConfiguration<ServiceOrderExpense>
    {
        public void Configure(EntityTypeBuilder<ServiceOrderExpense> builder)
        {
            builder.ToTable("ServiceOrderExpenses");
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.ServiceOrder)
                .WithMany()
                .HasForeignKey(e => e.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
