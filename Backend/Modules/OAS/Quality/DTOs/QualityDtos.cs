using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Quality.DTOs;

public class OasQualityCheckDto
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public string CheckType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public decimal QuantityChecked { get; set; }
    public decimal QuantityRejected { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
public class OasQualityCheckRequestDto
{
    [Required] public Guid ClientEventId { get; set; }
    [Required] public Guid PostId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? ChangeoverId { get; set; }
    public Guid? TemplateId { get; set; }
    [Required] public string CheckType { get; set; } = string.Empty;
    [Required] public string Result { get; set; } = string.Empty;
    public decimal QuantityChecked { get; set; } = 1;
    public decimal QuantityRejected { get; set; }
    public Guid? CauseId { get; set; }
    public string? Note { get; set; }
    [Required] public DateTimeOffset OccurredAt { get; set; }
}

public class OasQualityCheckTemplateDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CheckType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
public class OasQualityCheckTemplateRequestDto
{
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string CheckType { get; set; } = string.Empty;
}

public class OasQualityCheckTemplateItemDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
}
public class OasQualityCheckTemplateItemRequestDto
{
    [Required] public string Label { get; set; } = string.Empty;
    public string ValueType { get; set; } = "boolean";
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsRequired { get; set; } = true;
    public int SortOrder { get; set; }
}
