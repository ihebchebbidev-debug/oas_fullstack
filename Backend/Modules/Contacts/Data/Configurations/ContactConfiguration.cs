using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Contacts.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Contacts.Data.Configurations
{
    public class ContactConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Contact>(entity =>
            {
                entity.ToTable("Contacts");
                entity.HasKey(e => e.Id);
                // Non-unique index; the authoritative uniqueness is enforced by
                // partial unique index UX_Contacts_Tenant_Email_Active in
                // 20260725_Modules_Deep_Hardening.sql (tenant-scoped,
                // case-insensitive, excluding soft-deleted rows and NULL emails).
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.FirstName);
                entity.HasIndex(e => e.LastName);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.IsDeleted);
                entity.HasIndex(e => e.CreatedDate);
                entity.HasIndex(e => e.Cin);
                entity.HasIndex(e => e.MatriculeFiscale);

                entity.Property(e => e.CreatedDate).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                
                // Ignore the ContactNotes navigation - relationship defined in ContactNoteConfiguration
                // Ignore the TagAssignments navigation - relationship defined in ContactTagAssignmentConfiguration
            });
        }
    }
}
