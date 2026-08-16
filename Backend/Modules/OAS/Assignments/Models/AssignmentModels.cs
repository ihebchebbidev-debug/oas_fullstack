using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Assignments.Models;

/// <summary>v15 decision: PublishedAt lives on EACH row (per-post publish), never a single global flag — editing one post must not un-publish the whole board.</summary>
public class OasAssignment : OasEntityBase
{
    public DateOnly WorkDate { get; set; }
    public Guid ShiftTemplateId { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? AssignedBy { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public class OasPresenceEntry : OasEntityBase
{
    public Guid UserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public Guid ShiftTemplateId { get; set; }
    public string Status { get; set; } = "expected"; // expected|confirmed|absent
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? Reason { get; set; }
}
