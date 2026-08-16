using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Equipments.Models;

/// <summary>Maps to Postgres native enum `oas_criticality` — registered in OasNpgsqlEnums. Shared across Equipments/Causes/Events/SlaRules.</summary>
public enum OasCriticality { low, medium, high, critical }

public class OasEquipment : OasEntityBase, IOasSoftDeletable
{
    public Guid? PostId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public DateOnly? CommissionedAt { get; set; }
    public OasCriticality Criticality { get; set; } = OasCriticality.medium;
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsDeleted { get; set; }
}
