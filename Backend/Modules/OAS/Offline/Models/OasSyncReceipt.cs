namespace MyApi.Modules.OAS.Offline.Models;

/// <summary>
/// Diagnostic trail only (spec §5.1 comment: "Traçabilité du rejeu
/// hors-ligne") — the actual idempotent writes happen on each resource's
/// own endpoint (declarations, events, ...), keyed by their own
/// clientEventId. This table just records that a push happened, when,
/// and whether it was a first-time write or a replay, so support can
/// diagnose a device that kept retrying.
/// </summary>
public class OasSyncReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TenantId { get; set; }
    public Guid ClientEventId { get; set; }
    public string? DeviceId { get; set; }
    public string Entity { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Attempts { get; set; } = 1;
    public string Status { get; set; } = "ok"; // ok|duplicate|rejected
    public string? Error { get; set; }
}
