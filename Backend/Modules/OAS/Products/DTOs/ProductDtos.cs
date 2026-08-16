using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Products.DTOs;

public class OasProductDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Customer { get; set; }
    public string Unit { get; set; } = string.Empty;
}
public class OasProductRequestDto
{
    [Required] public string Reference { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    public string? Customer { get; set; }
    public string Unit { get; set; } = "pcs";
}

public class OasProductionOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid? LineId { get; set; }
    public decimal QuantityPlanned { get; set; }
    public decimal QuantityProduced { get; set; }
    public decimal QuantityScrapped { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
}
public class OasProductionOrderRequestDto
{
    [Required] public string OrderNumber { get; set; } = string.Empty;
    [Required] public Guid ProductId { get; set; }
    public Guid? LineId { get; set; }
    public decimal QuantityPlanned { get; set; }
    public DateOnly? DueDate { get; set; }
    public int Priority { get; set; }
}
public class OasProductionOrderStatusRequestDto { [Required] public string Status { get; set; } = string.Empty; }
