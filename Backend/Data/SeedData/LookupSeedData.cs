using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Lookups.Models;

namespace MyApi.Data.SeedData
{
    public class LookupSeedData
    {
        public void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LookupItem>().HasData(
                // NOTE: ContactType is intentionally NOT seeded as a lookup — contact
                // type is a fixed enum in the app ("individual" | "company" | "partner"
                // | "supplier") enforced at the frontend and by the Contacts service.
                // Do not re-add ContactType rows here; they would drift from the enum.

                // Contact Status
                new LookupItem { Id = 3, LookupType = "ContactStatus", Name = "active", Description = "Active Contact", SortOrder = 1, IsActive = true, CreatedUser = "system" },
                new LookupItem { Id = 4, LookupType = "ContactStatus", Name = "inactive", Description = "Inactive Contact", SortOrder = 2, IsActive = true, CreatedUser = "system" },
                new LookupItem { Id = 5, LookupType = "ContactStatus", Name = "prospect", Description = "Prospect", SortOrder = 3, IsActive = true, CreatedUser = "system" },
                new LookupItem { Id = 6, LookupType = "ContactStatus", Name = "customer", Description = "Customer", SortOrder = 4, IsActive = true, CreatedUser = "system" },
                
                // Article Categories  
                new LookupItem { Id = 7, LookupType = "ArticleCategory", Name = "hardware", Description = "Hardware", SortOrder = 1, IsActive = true, CreatedUser = "system" },
                new LookupItem { Id = 8, LookupType = "ArticleCategory", Name = "software", Description = "Software", SortOrder = 2, IsActive = true, CreatedUser = "system" },
                new LookupItem { Id = 9, LookupType = "ArticleCategory", Name = "service", Description = "Service", SortOrder = 3, IsActive = true, CreatedUser = "system" },
                
                // Article Status
                new LookupItem { Id = 10, LookupType = "ArticleStatus", Name = "active", Description = "Active", SortOrder = 1, IsActive = true, CreatedUser = "system" },
                new LookupItem { Id = 11, LookupType = "ArticleStatus", Name = "inactive", Description = "Inactive", SortOrder = 2, IsActive = true, CreatedUser = "system" },
                new LookupItem { Id = 12, LookupType = "ArticleStatus", Name = "discontinued", Description = "Discontinued", SortOrder = 3, IsActive = true, CreatedUser = "system" }
            );
        }
    }
}
