using MyApi.Modules.OAS.Common.Data;
using MyApi.Modules.OAS.Equipments.Models;

namespace MyApi.Modules.OAS.Causes.Models;

public enum OasCauseDomain { stop, scrap, root_cause }
public enum OasEventType { technical_stop, quality_stop, material_wait, changeover, other_stop }

public class OasCause : OasEntityBase
{
    public Guid? ParentId { get; set; }
    public OasCauseDomain Domain { get; set; }
    public string Code { get; set; } = string.Empty;
    public string LabelFr { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public OasEventType? EventType { get; set; }
    public OasCriticality DefaultCriticality { get; set; } = OasCriticality.medium;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class OasCauseProposal : OasEntityBase
{
    public OasCauseDomain Domain { get; set; }
    public string LabelFr { get; set; } = string.Empty;
    public string? LabelAr { get; set; }
    public Guid ProposedBy { get; set; }
    public string Status { get; set; } = "pending"; // pending|accepted|rejected
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ResultingCauseId { get; set; }
}
