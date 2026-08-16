using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Declarations.Models;

public enum OasDeclarationKind { production, scrap, rework }

/// <summary>
/// Append-only (spec §5.1, trigger `trg_oas_decl_immutable`): the DB
/// refuses any UPDATE that touches Id/QuantityOk/QuantityNok/OccurredAt.
/// A correction is always a NEW row with CorrectsId set — this C# layer
/// never attempts to update those columns on an existing row, matching
/// the trigger by construction, not just by convention.
/// </summary>
public class OasDeclaration : OasEntityBase
{
    public Guid ClientEventId { get; set; }
    public OasDeclarationKind Kind { get; set; }
    public Guid? PostSessionId { get; set; }
    public Guid PostId { get; set; }
    public Guid? LineId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal QuantityOk { get; set; }
    public decimal QuantityNok { get; set; }
    public Guid? ScrapCauseId { get; set; }
    public string? PhotoPath { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CorrectsId { get; set; }
    public bool IsCorrected { get; set; }
    public string? CorrectionReason { get; set; }
    public Guid CreatedBy { get; set; }
}
