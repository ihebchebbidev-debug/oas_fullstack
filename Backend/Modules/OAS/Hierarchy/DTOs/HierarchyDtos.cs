using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Hierarchy.DTOs;

public class OasSiteDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool Archived { get; set; }
}
public class OasSiteRequestDto
{
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    public string? Timezone { get; set; }
    public string? Address { get; set; }
}

public class OasZoneDto
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool Archived { get; set; }
}
public class OasZoneRequestDto
{
    [Required] public Guid SiteId { get; set; }
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class OasLineDto
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public decimal? TargetOee { get; set; }
    public bool Archived { get; set; }
}
public class OasLineRequestDto
{
    [Required] public Guid ZoneId { get; set; }
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public decimal? TargetOee { get; set; }
}

public class OasPostDto
{
    public Guid Id { get; set; }
    public Guid LineId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? PostType { get; set; }
    public bool IsCritical { get; set; }
    public bool IsActive { get; set; }
    public bool Archived { get; set; }
}
public class OasPostRequestDto
{
    [Required] public Guid LineId { get; set; }
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? PostType { get; set; }
}
public class OasPostAttributesRequestDto { public string? PostType { get; set; } public int? SortOrder { get; set; } }
public class OasPostCriticalRequestDto { public bool IsCritical { get; set; } }

public class OasHierarchyTreeSiteDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<OasHierarchyTreeZoneDto> Zones { get; set; } = new();
}
public class OasHierarchyTreeZoneDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<OasHierarchyTreeLineDto> Lines { get; set; } = new();
}
public class OasHierarchyTreeLineDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<OasPostDto> Posts { get; set; } = new();
}

public class OasQrTokenDto { public Guid PostId { get; set; } public string Token { get; set; } = string.Empty; }

public class OasCompletenessDto
{
    public double PostsRatio { get; set; }
    public double NamedProductsRatio { get; set; }
    public double ReferencesWithCadenceRatio { get; set; }
    public double CausesRatio { get; set; }
    public double ShiftCoverageRatio { get; set; }
    public double Overall { get; set; }
}

public class OasPostLayoutDto
{
    public Guid PostId { get; set; }
    public string LayoutKey { get; set; } = "default";
    public int SortOrder { get; set; }
    public int ColSpan { get; set; }
    public int RowSpan { get; set; }
    public decimal? X { get; set; }
    public decimal? Y { get; set; }
}
