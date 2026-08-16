using MyApi.Modules.OAS.Causes.Models;
using MyApi.Modules.OAS.Common.Data;
using MyApi.Modules.OAS.Equipments.Models;
using MyApi.Modules.OAS.Events.Models;

namespace MyApi.Modules.OAS.Sla.Models;

public class OasSlaRule : OasEntityBase
{
    public OasEventType EventType { get; set; }
    public OasCriticality? Criticality { get; set; }
    public Guid? LineId { get; set; }
    public int TargetMin { get; set; } = 10;
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

public class OasEscalation : OasEntityBase
{
    public Guid EventId { get; set; }
    public OasEscalationLevel Level { get; set; }
    public DateTimeOffset TriggeredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedBy { get; set; }
}

/// <summary>v15 critical fix target: this must ALWAYS be keyed by the real authenticated oas_users.Id, never a hardcoded name like the old client's `CURRENT_RESPONDER = 'Karim T.'`.</summary>
public class OasResponderAvailability : OasEntityBase
{
    public Guid ProfileId { get; set; }
    public bool Busy { get; set; }
    public DateTimeOffset Since { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
}
