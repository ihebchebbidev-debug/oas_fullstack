using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.Planning.Models;

namespace MyApi.Modules.Planning.Data
{
    public class PlannedLineEntryConfiguration : IEntityTypeConfiguration<PlannedLineEntry>
    {
        public void Configure(EntityTypeBuilder<PlannedLineEntry> b)
        {
            b.ToTable("PlannedLineEntries");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.ParentType, x.ParentId });
            b.HasIndex(x => new { x.TenantId, x.OriginOfferItemId });
        }
    }
}
