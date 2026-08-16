using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Imports.DTOs;

public class OasImportDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RowsTotal { get; set; }
    public int RowsOk { get; set; }
    public int RowsError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class OasImportRowDto { public int RowNumber { get; set; } public Dictionary<string, object?> Raw { get; set; } = new(); public string Status { get; set; } = string.Empty; public string? Error { get; set; } }

public class OasImportCreateRequestDto
{
    [Required] public string Kind { get; set; } = string.Empty; // datasetType — "products" | "causes" | "equipment"
    [Required] public List<Dictionary<string, object?>> Rows { get; set; } = new();
}

public class OasImportLineDto { public int RowNumber { get; set; } public string Status { get; set; } = string.Empty; public string? Error { get; set; } }
