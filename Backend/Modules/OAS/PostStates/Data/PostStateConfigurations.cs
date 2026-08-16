using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.PostStates.Models;

namespace MyApi.Modules.OAS.PostStates.Data;

public class OasPostStateConfiguration : IEntityTypeConfiguration<OasPostState>
{
    public void Configure(EntityTypeBuilder<OasPostState> b)
    {
        b.ToTable("oas_post_states");
        b.HasKey(x => x.PostId);
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.State).HasColumnName("state");
        b.Property(x => x.Since).HasColumnName("since");
        b.Property(x => x.ActiveEventId).HasColumnName("active_event_id");
        b.Property(x => x.ActiveSessionId).HasColumnName("active_session_id");
        b.Property(x => x.ActiveChangeoverId).HasColumnName("active_changeover_id");
        b.Property(x => x.CurrentUserId).HasColumnName("current_user_id");
        b.Property(x => x.CurrentProductId).HasColumnName("current_product_id");
        b.Property(x => x.CurrentOrderId).HasColumnName("current_order_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        // Read-only from EF's perspective — the trigger owns all writes.
        b.ToTable(t => t.ExcludeFromMigrations());
    }
}

public class OasPostStateHistoryConfiguration : IEntityTypeConfiguration<OasPostStateHistory>
{
    public void Configure(EntityTypeBuilder<OasPostStateHistory> b)
    {
        b.ToTable("oas_post_state_history");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.State).HasColumnName("state");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.EndedAt).HasColumnName("ended_at");
        b.Property(x => x.DurationSec).HasColumnName("duration_sec");
    }
}
