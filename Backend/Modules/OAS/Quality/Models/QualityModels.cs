using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Quality.Models;

public enum OasCheckType { first_part, in_process, final }
public enum OasCheckResult { ok, rework, scrap }

public class OasQualityCheck : OasEntityBase
{
    public Guid ClientEventId { get; set; }
    public Guid PostId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? ChangeoverId { get; set; }
    public Guid? EventId { get; set; }
    public Guid? TemplateId { get; set; }
    public OasCheckType CheckType { get; set; }
    public OasCheckResult Result { get; set; }
    public decimal QuantityChecked { get; set; } = 1;
    public decimal QuantityRejected { get; set; }
    public Guid? CauseId { get; set; }
    public string? PhotoPath { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid InspectorId { get; set; }
}

public class OasQualityCheckTemplate : OasEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public OasCheckType CheckType { get; set; }
    public bool IsActive { get; set; } = true;
}

public class OasQualityCheckTemplateItem : OasEntityBase
{
    public Guid TemplateId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string ValueType { get; set; } = "boolean"; // boolean|numeric|text
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsRequired { get; set; } = true;
    public int SortOrder { get; set; }
}
