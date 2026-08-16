using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Offline.DTOs;

public class OasSyncPushItemDto
{
    [Required] public Guid ClientEventId { get; set; }
    [Required] public string Entity { get; set; } = string.Empty;
    [Required] public DateTimeOffset OccurredAt { get; set; }
    public string? DeviceId { get; set; }
}
public class OasSyncPushRequestDto { [Required] public List<OasSyncPushItemDto> Items { get; set; } = new(); }

public class OasSyncReceiptDto
{
    public Guid ClientEventId { get; set; }
    public string Entity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public int Attempts { get; set; }
}
public class OasSyncPushResponseDto { public List<OasSyncReceiptDto> Receipts { get; set; } = new(); }

public class OasSyncPullEntryDto { public string Entity { get; set; } = string.Empty; public Guid Id { get; set; } public DateTimeOffset UpdatedAt { get; set; } }
public class OasSyncPullResponseDto { public List<OasSyncPullEntryDto> Changes { get; set; } = new(); public DateTimeOffset ServerTime { get; set; } = DateTimeOffset.UtcNow; }

public class OasAttachmentDto
{
    public Guid Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string? EntityTable { get; set; }
    public Guid? EntityId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public class OasAttachmentRequestDto
{
    [Required] public string Path { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string? EntityTable { get; set; }
    public Guid? EntityId { get; set; }
}
