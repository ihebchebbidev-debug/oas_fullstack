using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Changeovers.Models;

namespace MyApi.Modules.OAS.Changeovers.Data;

public class OasChangeoverConfiguration : IEntityTypeConfiguration<OasChangeover>
{
    public void Configure(EntityTypeBuilder<OasChangeover> b)
    {
        b.ToTable("oas_changeovers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ClientEventId).HasColumnName("client_event_id");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.FromProductId).HasColumnName("from_product_id");
        b.Property(x => x.ToProductId).HasColumnName("to_product_id");
        b.Property(x => x.ProductionOrderId).HasColumnName("production_order_id");
        b.Property(x => x.EventId).HasColumnName("event_id");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.FirstGoodPartAt).HasColumnName("first_good_part_at");
        b.Property(x => x.EndedAt).HasColumnName("ended_at");
        b.Property(x => x.DurationSec).HasColumnName("duration_sec");
        b.Property(x => x.TargetMin).HasColumnName("target_min");
        b.Property(x => x.Steps).HasColumnName("steps").HasColumnType("jsonb");
        b.Property(x => x.StartedBy).HasColumnName("started_by");
        b.Property(x => x.ValidatedBy).HasColumnName("validated_by");
        b.Property(x => x.CreatedAt).HasColumnName("received_at");
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => x.ClientEventId).IsUnique();
    }
}
