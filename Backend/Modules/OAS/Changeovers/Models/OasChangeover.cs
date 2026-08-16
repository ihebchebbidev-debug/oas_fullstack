using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Changeovers.Models;

public class OasChangeover : OasEntityBase
{
    public Guid ClientEventId { get; set; }
    public Guid PostId { get; set; }
    public Guid? FromProductId { get; set; }
    public Guid ToProductId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? EventId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FirstGoodPartAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int? DurationSec { get; set; }
    public int? TargetMin { get; set; }
    /// <summary>v13: JSON array of {id, label, done} — the 5-step checklist, persisted so a navigate-away/restart can resume it.</summary>
    public string Steps { get; set; } = "[]";
    public Guid StartedBy { get; set; }
    public Guid? ValidatedBy { get; set; }
}
