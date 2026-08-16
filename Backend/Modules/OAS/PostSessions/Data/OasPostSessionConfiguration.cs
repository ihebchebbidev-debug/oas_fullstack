using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.PostSessions.Models;

namespace MyApi.Modules.OAS.PostSessions.Data;

public class OasPostSessionConfiguration : IEntityTypeConfiguration<OasPostSession>
{
    public void Configure(EntityTypeBuilder<OasPostSession> b)
    {
        b.ToTable("oas_post_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ClientEventId).HasColumnName("client_event_id");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.AssignmentId).HasColumnName("assignment_id");
        b.Property(x => x.ProductionOrderId).HasColumnName("production_order_id");
        b.Property(x => x.ShiftTemplateId).HasColumnName("shift_template_id");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.EndedAt).HasColumnName("ended_at");
        b.Property(x => x.StartedVia).HasColumnName("started_via");
        b.Property(x => x.ReceivedAt).HasColumnName("received_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => x.ClientEventId).IsUnique();
    }
}
