using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Imports.Models;

public enum OasImportStatus { pending, committed, failed }

public class OasImport : OasEntityBase
{
    public string Kind { get; set; } = string.Empty;
    public OasImportStatus Status { get; set; } = OasImportStatus.pending;
    public string? FilePath { get; set; }
    public int RowsTotal { get; set; }
    public int RowsOk { get; set; }
    public int RowsError { get; set; }
    public string Report { get; set; } = "{}";
    public Guid? ImportedBy { get; set; }
    public DateTimeOffset? CommittedAt { get; set; }
}

public class OasImportLine : OasEntityBase
{
    public Guid ImportId { get; set; }
    public int RowNumber { get; set; }
    public string Raw { get; set; } = "{}";
    public string Status { get; set; } = "pending"; // pending|ok|error
    public string? Error { get; set; }
}
