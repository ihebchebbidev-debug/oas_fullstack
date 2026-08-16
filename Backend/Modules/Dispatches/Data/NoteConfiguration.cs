using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Dispatches.Models;

namespace MyApi.Modules.Dispatches.Data
{
    public class NoteConfiguration : IEntityTypeConfiguration<Note>
    {
        public void Configure(EntityTypeBuilder<Note> builder)
        {
            builder.ToTable("Notes");
            builder.HasKey(n => n.Id);
            builder.Property(n => n.DispatchId).IsRequired();
            builder.Property(n => n.Content).HasColumnName("NoteText").IsRequired();
            builder.Property(n => n.NoteType).HasMaxLength(50).IsRequired();
            builder.Property(n => n.IsInternal);
            builder.Property(n => n.CreatedBy).HasMaxLength(100).IsRequired();
        }
    }
}
