using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Hierarchy.Models;

public class OasSite : OasEntityBase, IOasSoftDeletable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Timezone { get; set; } = "Africa/Tunis";
    public string? Address { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class OasZone : OasEntityBase, IOasSoftDeletable
{
    public Guid SiteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class OasLine : OasEntityBase, IOasSoftDeletable
{
    public Guid ZoneId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public decimal? TargetOee { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class OasPost : OasEntityBase, IOasSoftDeletable
{
    public Guid LineId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string QrToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset? QrRotatedAt { get; set; }
    public int SortOrder { get; set; }
    public string? PostType { get; set; }
    public bool IsCritical { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Spec §5.2 #14 — sort_order/layout_key delivered now; free-form x/y editor is §13 point 4, "à trancher" (not built without an explicit ask).</summary>
public class OasPostLayout : OasEntityBase
{
    public Guid PostId { get; set; }
    public string LayoutKey { get; set; } = "default";
    public int SortOrder { get; set; }
    public int ColSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public decimal? X { get; set; }
    public decimal? Y { get; set; }
}
