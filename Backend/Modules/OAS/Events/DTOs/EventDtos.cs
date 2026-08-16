using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Events.DTOs;

public class OasEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid PostId { get; set; }
    public Guid? LineId { get; set; }
    public Guid? CauseId { get; set; }
    public string Criticality { get; set; } = string.Empty;
    public Guid DeclaredBy { get; set; }
    public DateTimeOffset DeclaredAt { get; set; }
    public Guid? AssigneeId { get; set; }
    public int? EtaMinutes { get; set; }
    public int SlaMinutes { get; set; }
    public DateTimeOffset? SlaDueAt { get; set; }
    public bool SlaBreached { get; set; }
    public string EscalationLevel { get; set; } = string.Empty;
    public DateTimeOffset? ClosedAt { get; set; }
}

public class OasCreateEventRequestDto
{
    [Required] public Guid ClientEventId { get; set; }
    [Required] public string EventType { get; set; } = string.Empty;
    [Required] public Guid PostId { get; set; }
    public Guid? CauseId { get; set; }
    public string? Criticality { get; set; }
    public Guid? PostSessionId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? EquipmentId { get; set; }
    public string? Note { get; set; }
    [Required] public DateTimeOffset DeclaredAt { get; set; }
}

public class OasEtaRequestDto { [Required] public int EtaMinutes { get; set; } }
public class OasRequalifyRequestDto { [Required] public string EventType { get; set; } = string.Empty; }
public class OasCloseEventRequestDto { [Required] public string ClosureType { get; set; } = string.Empty; public string? Note { get; set; } }
public class OasCancelEventRequestDto { public string? Reason { get; set; } }

public class OasEventTransitionDto
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
