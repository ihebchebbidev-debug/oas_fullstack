using MyApi.Modules.OAS.Causes.Models;
using MyApi.Modules.OAS.Common.Data;
using MyApi.Modules.OAS.Equipments.Models;
using MyApi.Modules.OAS.ShopFloorAuth.Models;

namespace MyApi.Modules.OAS.Events.Models;

public enum OasEventStatus { declared, notified, acknowledged, on_site, resolved, closed, cancelled }
public enum OasClosureType { resolved, palliative, no_fault, cancelled }
public enum OasEscalationLevel { none, level_1, level_2 }
public enum OasNotifyResponse { coming, busy, delegated }

public class OasEvent : OasEntityBase
{
    public Guid ClientEventId { get; set; }
    public OasEventType EventType { get; set; }
    public OasEventStatus Status { get; set; } = OasEventStatus.declared;

    public Guid PostId { get; set; }
    public Guid? LineId { get; set; }
    public Guid? ZoneId { get; set; }
    public Guid? SiteId { get; set; }

    public Guid? PostSessionId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? EquipmentId { get; set; }

    public Guid? CauseId { get; set; }
    public Guid? RootCauseId { get; set; }
    public OasCriticality Criticality { get; set; } = OasCriticality.medium;

    public Guid DeclaredBy { get; set; }
    public DateTimeOffset DeclaredAt { get; set; }
    public DateTimeOffset? NotifiedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedBy { get; set; }
    public int? EtaMinutes { get; set; }
    public DateTimeOffset? OnSiteAt { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public OasClosureType? ClosureType { get; set; }
    public string? ClosureNote { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public int SlaMinutes { get; set; } = 10;
    public DateTimeOffset? SlaDueAt { get; set; }
    public bool SlaBreached { get; set; }
    public OasEscalationLevel EscalationLevel { get; set; } = OasEscalationLevel.none;

    public int? DurationSec { get; set; }
    public int? ResponseSec { get; set; }
    public int? RepairSec { get; set; }

    public string? Note { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class OasEventTransition : OasEntityBase
{
    public Guid EventId { get; set; }
    public OasEventStatus? FromStatus { get; set; }
    public OasEventStatus ToStatus { get; set; }
    public Guid? ActorId { get; set; }
    public OasAppRole? ActorRole { get; set; }
    public string Payload { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public class OasEventNotification : OasEntityBase
{
    public Guid EventId { get; set; }
    public Guid? RecipientUserId { get; set; }
    public OasAppRole? RecipientRole { get; set; }
    public string Channel { get; set; } = "push";
    public OasEscalationLevel EscalationLevel { get; set; } = OasEscalationLevel.none;
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public OasNotifyResponse? Response { get; set; }
    public int? EtaMinutes { get; set; }
}
