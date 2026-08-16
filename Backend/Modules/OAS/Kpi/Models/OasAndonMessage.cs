namespace MyApi.Modules.OAS.Kpi.Models;

/// <summary>v13: `LineId = null` means site-wide (the old client's MOTD had no line association at all — the initial `?lineId=` required contract didn't cover that).</summary>
public class OasAndonMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TenantId { get; set; }
    public Guid? LineId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
