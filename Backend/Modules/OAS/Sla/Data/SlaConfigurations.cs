using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Sla.Models;

namespace MyApi.Modules.OAS.Sla.Data;

public class OasSlaRuleConfiguration : IEntityTypeConfiguration<OasSlaRule>
{
    public void Configure(EntityTypeBuilder<OasSlaRule> b)
    {
        b.ToTable("oas_sla_rules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.EventType).HasColumnName("event_type");
        b.Property(x => x.Criticality).HasColumnName("criticality");
        b.Property(x => x.LineId).HasColumnName("line_id");
        b.Property(x => x.TargetMin).HasColumnName("target_min");
        b.Property(x => x.Priority).HasColumnName("priority");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.EventType, x.IsActive, x.Priority });
    }
}

public class OasEscalationConfiguration : IEntityTypeConfiguration<OasEscalation>
{
    public void Configure(EntityTypeBuilder<OasEscalation> b)
    {
        b.ToTable("oas_escalations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.EventId).HasColumnName("event_id");
        b.Property(x => x.Level).HasColumnName("level");
        b.Property(x => x.TriggeredAt).HasColumnName("triggered_at");
        b.Property(x => x.Reason).HasColumnName("reason");
        b.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
        b.Property(x => x.AcknowledgedBy).HasColumnName("acknowledged_by");
        b.Ignore(x => x.CreatedAt);
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.EventId, x.TriggeredAt });
    }
}

public class OasResponderAvailabilityConfiguration : IEntityTypeConfiguration<OasResponderAvailability>
{
    public void Configure(EntityTypeBuilder<OasResponderAvailability> b)
    {
        b.ToTable("oas_responder_availability");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ProfileId).HasColumnName("profile_id");
        b.Property(x => x.Busy).HasColumnName("busy");
        b.Property(x => x.Since).HasColumnName("since");
        b.Property(x => x.Reason).HasColumnName("reason");
        b.Ignore(x => x.CreatedAt);
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TenantId, x.ProfileId }).IsUnique();
    }
}
