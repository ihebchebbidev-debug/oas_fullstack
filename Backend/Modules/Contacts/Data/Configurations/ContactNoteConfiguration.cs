using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Contacts.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Contacts.Data.Configurations
{
    public class ContactNoteConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ContactNote>(entity =>
            {
                entity.ToTable("ContactNotes");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.ContactId).IsRequired();
                entity.Property(e => e.Note).IsRequired();
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("NOW()");
                
                entity.HasIndex(e => e.ContactId);
                entity.HasIndex(e => e.CreatedDate);

                // Explicitly configure the relationship to use the existing ContactId column
                entity.HasOne(n => n.Contact)
                    .WithMany(c => c.ContactNotes)
                    .HasForeignKey(n => n.ContactId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
