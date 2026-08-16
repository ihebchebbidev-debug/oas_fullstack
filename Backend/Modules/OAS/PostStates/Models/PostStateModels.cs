using MyApi.Modules.OAS.Common.Data;
using MyApi.Modules.OAS.Hierarchy.Models;

namespace MyApi.Modules.OAS.PostStates.Models;

public enum OasPostStateValue { production, material_wait, changeover, technical_stop, quality_stop, unassigned }

/// <summary>
/// Read-only from C#'s perspective — populated entirely by the
/// oas_recompute_post_state trigger (spec §3: "logique métier critique
/// conservée dans les triggers Postgres... jamais réécrite en C#"). This
/// module only ever SELECTs from these tables.
/// </summary>
public class OasPostState : IOasTenantEntity
{
    public Guid PostId { get; set; } // PK, references oas_posts(id)
    public int TenantId { get; set; }
    public OasPostStateValue State { get; set; } = OasPostStateValue.unassigned;
    public DateTimeOffset Since { get; set; }
    public Guid? ActiveEventId { get; set; }
    public Guid? ActiveSessionId { get; set; }
    public Guid? ActiveChangeoverId { get; set; }
    public Guid? CurrentUserId { get; set; }
    public Guid? CurrentProductId { get; set; }
    public Guid? CurrentOrderId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class OasPostStateHistory
{
    public Guid Id { get; set; }
    public int TenantId { get; set; }
    public Guid PostId { get; set; }
    public OasPostStateValue State { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int? DurationSec { get; set; }
}
