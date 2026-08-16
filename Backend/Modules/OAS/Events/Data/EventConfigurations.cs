using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Events.Models;

namespace MyApi.Modules.OAS.Events.Data;

public class OasEventConfiguration : IEntityTypeConfiguration<OasEvent>
{
    public void Configure(EntityTypeBuilder<OasEvent> b)
    {
        b.ToTable("oas_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ClientEventId).HasColumnName("client_event_id");
        b.Property(x => x.EventType).HasColumnName("event_type");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.LineId).HasColumnName("line_id");
        b.Property(x => x.ZoneId).HasColumnName("zone_id");
        b.Property(x => x.SiteId).HasColumnName("site_id");
        b.Property(x => x.PostSessionId).HasColumnName("post_session_id");
        b.Property(x => x.ProductionOrderId).HasColumnName("production_order_id");
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.Property(x => x.EquipmentId).HasColumnName("equipment_id");
        b.Property(x => x.CauseId).HasColumnName("cause_id");
        b.Property(x => x.RootCauseId).HasColumnName("root_cause_id");
        b.Property(x => x.Criticality).HasColumnName("criticality");
        b.Property(x => x.DeclaredBy).HasColumnName("declared_by");
        b.Property(x => x.DeclaredAt).HasColumnName("declared_at");
        b.Property(x => x.NotifiedAt).HasColumnName("notified_at");
        b.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
        b.Property(x => x.AcknowledgedBy).HasColumnName("acknowledged_by");
        b.Property(x => x.EtaMinutes).HasColumnName("eta_minutes");
        b.Property(x => x.OnSiteAt).HasColumnName("on_site_at");
        b.Property(x => x.AssigneeId).HasColumnName("assignee_id");
        b.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
        b.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
        b.Property(x => x.ClosureType).HasColumnName("closure_type");
        b.Property(x => x.ClosureNote).HasColumnName("closure_note");
        b.Property(x => x.ClosedAt).HasColumnName("closed_at");
        b.Property(x => x.ClosedBy).HasColumnName("closed_by");
        b.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        b.Property(x => x.CancelReason).HasColumnName("cancel_reason");
        b.Property(x => x.SlaMinutes).HasColumnName("sla_minutes");
        b.Property(x => x.SlaDueAt).HasColumnName("sla_due_at");
        b.Property(x => x.SlaBreached).HasColumnName("sla_breached");
        b.Property(x => x.EscalationLevel).HasColumnName("escalation_level");
        b.Property(x => x.DurationSec).HasColumnName("duration_sec");
        b.Property(x => x.ResponseSec).HasColumnName("response_sec");
        b.Property(x => x.RepairSec).HasColumnName("repair_sec");
        b.Property(x => x.Note).HasColumnName("note");
        b.Property(x => x.ReceivedAt).HasColumnName("received_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Ignore(x => x.CreatedAt);
        b.HasIndex(x => x.ClientEventId).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Status });
        b.HasIndex(x => new { x.TenantId, x.PostId, x.DeclaredAt });
    }
}

public class OasEventTransitionConfiguration : IEntityTypeConfiguration<OasEventTransition>
{
    public void Configure(EntityTypeBuilder<OasEventTransition> b)
    {
        b.ToTable("oas_event_transitions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.EventId).HasColumnName("event_id");
        b.Property(x => x.FromStatus).HasColumnName("from_status");
        b.Property(x => x.ToStatus).HasColumnName("to_status");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.ActorRole).HasColumnName("actor_role");
        b.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        b.Ignore(x => x.CreatedAt);
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.EventId, x.OccurredAt });
    }
}

public class OasEventNotificationConfiguration : IEntityTypeConfiguration<OasEventNotification>
{
    public void Configure(EntityTypeBuilder<OasEventNotification> b)
    {
        b.ToTable("oas_event_notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.EventId).HasColumnName("event_id");
        b.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id");
        b.Property(x => x.RecipientRole).HasColumnName("recipient_role");
        b.Property(x => x.Channel).HasColumnName("channel");
        b.Property(x => x.EscalationLevel).HasColumnName("escalation_level");
        b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
        b.Property(x => x.ReadAt).HasColumnName("read_at");
        b.Property(x => x.RespondedAt).HasColumnName("responded_at");
        b.Property(x => x.Response).HasColumnName("response");
        b.Property(x => x.EtaMinutes).HasColumnName("eta_minutes");
        b.Ignore(x => x.CreatedAt);
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TenantId, x.RecipientUserId, x.SentAt });
    }
}
