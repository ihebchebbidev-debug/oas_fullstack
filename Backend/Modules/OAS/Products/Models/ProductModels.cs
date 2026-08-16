using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Products.Models;

public enum OasOrderStatus { planned, in_progress, done, cancelled }

public class OasProduct : OasEntityBase, IOasSoftDeletable
{
    public string Reference { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Customer { get; set; }
    public string Unit { get; set; } = "pcs";
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class OasProductionOrder : OasEntityBase
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid? LineId { get; set; }
    public decimal QuantityPlanned { get; set; }
    public decimal QuantityProduced { get; set; }
    public decimal QuantityScrapped { get; set; }
    public DateOnly? DueDate { get; set; }
    public OasOrderStatus Status { get; set; } = OasOrderStatus.planned;
    public int Priority { get; set; }
}
