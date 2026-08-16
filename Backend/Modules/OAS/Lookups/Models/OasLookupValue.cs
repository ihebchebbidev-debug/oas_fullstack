using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Lookups.Models;

/// <summary>
/// Flat lookup lists (spec §7.2): PostType, PostCriticality, EquipmentType,
/// CadenceUnit, ProductFamily, PackagingUnit, ScrapMotif, QualityDefect,
/// ChangeoverType, PresenceStatus, AbsenceReason, InterventionOutcome,
/// ImportSource, ShiftLabel, SiteType, ZoneType — one table, `type`
/// discriminates. The socle's LookupsController is read-only and
/// hardcoded (spec §3.2: never extended); this is OAS's own.
/// </summary>
public class OasLookupValue : OasEntityBase
{
    public string Type { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}
