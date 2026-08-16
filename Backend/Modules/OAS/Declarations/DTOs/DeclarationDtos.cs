using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Declarations.DTOs;

public class OasDeclarationDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid? PostSessionId { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal QuantityOk { get; set; }
    public decimal QuantityNok { get; set; }
    public Guid? ScrapCauseId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? CorrectsId { get; set; }
    public bool IsCorrected { get; set; }
    public string? CorrectionReason { get; set; }
    public Guid CreatedBy { get; set; }
}

public class OasProductionDeclarationRequestDto
{
    [Required] public Guid ClientEventId { get; set; }
    public Guid? PostSessionId { get; set; }
    [Required] public Guid PostId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal QuantityOk { get; set; }
    public decimal QuantityNok { get; set; }
    public string? Note { get; set; }
    [Required] public DateTimeOffset OccurredAt { get; set; }
}

public class OasScrapDeclarationRequestDto
{
    [Required] public Guid ClientEventId { get; set; }
    public Guid? PostSessionId { get; set; }
    [Required] public Guid PostId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ProductId { get; set; }
    [Required] public decimal QuantityNok { get; set; }
    public Guid? ScrapCauseId { get; set; }
    public string? Note { get; set; }
    [Required] public DateTimeOffset OccurredAt { get; set; }
}

public class OasCorrectDeclarationRequestDto
{
    [Required] public Guid ClientEventId { get; set; }
    public decimal? QuantityOk { get; set; }
    public decimal? QuantityNok { get; set; }
    [Required] public string Reason { get; set; } = string.Empty;
}
