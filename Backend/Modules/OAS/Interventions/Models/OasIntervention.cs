using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Interventions.Models;

public class OasIntervention : OasEntityBase
{
    public Guid EventId { get; set; }
    public Guid? AssigneeId { get; set; }
    public string Status { get; set; } = "open"; // open|in_progress|closed
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
