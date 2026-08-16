using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Dispatches.Models;

namespace MyApi.Modules.Dispatches.Data
{
    public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
    {
        public void Configure(EntityTypeBuilder<Expense> builder)
        {
            builder.ToTable("Expenses");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.DispatchId).IsRequired();
            builder.Property(e => e.ExpenseType).HasMaxLength(50).IsRequired();
            builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(e => e.Description).HasMaxLength(500);
            builder.Property(e => e.ExpenseDate).IsRequired();
            builder.Property(e => e.ReceiptPath).HasMaxLength(500);
            builder.Property(e => e.CreatedDate).IsRequired();
            builder.Property(e => e.OverrunFlag).HasDefaultValue(false);
            builder.Property(e => e.OverrunReason).HasMaxLength(500);
        }
    }
}
