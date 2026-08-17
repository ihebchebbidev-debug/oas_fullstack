using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Offline.Models;

/// <summary>
/// Metadata-only, standalone (spec §3.2 forbids referencing the socle's
/// Documents module/types directly). The actual file bytes go wherever
/// the client already uploads them (UploadThing / local /uploads via the
/// socle's existing generic upload endpoint, spec §1.1) — this table just
/// records the resulting path against an OAS entity.
/// </summary>
public class OasAttachment : IOasTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TenantId { get; set; }
    public string Bucket { get; set; } = "oas";
    public string Path { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string? EntityTable { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? UploadedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
