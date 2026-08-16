using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Sla.DTOs;

public class OasSlaRuleDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Criticality { get; set; }
    public Guid? LineId { get; set; }
    public int TargetMin { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}
public class OasSlaRuleRequestDto
{
    [Required] public string EventType { get; set; } = string.Empty;
    public string? Criticality { get; set; }
    public Guid? LineId { get; set; }
    [Required] public int TargetMin { get; set; }
    public int Priority { get; set; }
}

public class OasEscalationDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTimeOffset TriggeredAt { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
}

public class OasResponderAvailabilityDto { public Guid ProfileId { get; set; } public bool Busy { get; set; } public string? Reason { get; set; } }
public class OasResponderAvailabilityRequestDto { public bool Busy { get; set; } public string? Reason { get; set; } }
