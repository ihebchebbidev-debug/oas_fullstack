using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Cadences.Models;

/// <summary>Current cadence per (product, post) — spec §5.1, `oas_routings` reused. Always the latest; history lives in OasRoutingVersion.</summary>
public class OasRouting : OasEntityBase, IOasTenantEntity
{
    public Guid ProductId { get; set; }
    public Guid PostId { get; set; }
    public decimal Rate { get; set; } = 60;
    public decimal? CycleTimeSec { get; set; }
    public int? ChangeoverTargetMin { get; set; }
    public int OperatorsRequired { get; set; } = 1;
}

/// <summary>Spec §5.2 #2 — versioned cadence history, since oas_routings has no version column.</summary>
public class OasRoutingVersion : OasEntityBase
{
    public Guid ProductId { get; set; }
    public Guid PostId { get; set; }
    public decimal Rate { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset Since { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
}
