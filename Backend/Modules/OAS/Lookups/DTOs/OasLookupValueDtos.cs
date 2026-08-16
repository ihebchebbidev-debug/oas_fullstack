using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Lookups.DTOs;

public class OasLookupValueDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
}

public class OasLookupValueRequestDto
{
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Label { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
}
