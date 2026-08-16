namespace MyApi.Modules.EmailAccounts.DTOs
{
    // ─── Synced Email DTOs ───

    public class SyncedEmailDto
    {
        public Guid Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string? ThreadId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? Snippet { get; set; }
        public string FromEmail { get; set; } = string.Empty;
        public string? FromName { get; set; }
        public string? ToEmails { get; set; }    // JSON array
        public string? CcEmails { get; set; }    // JSON array
        public string? BodyPreview { get; set; }
        public bool IsRead { get; set; }
        public bool IsStarred { get; set; }
        public bool HasAttachments { get; set; }
        public string? Labels { get; set; }      // JSON array
        public DateTime ReceivedAt { get; set; }
        public List<SyncedEmailAttachmentDto> Attachments { get; set; } = new();
    }

    public class SyncedEmailsPageDto
    {
        public List<SyncedEmailDto> Emails { get; set; } = new();
        public int TotalCount { get; set; }
        public string? NextPageToken { get; set; }
    }

    public class SyncResultDto
    {
        public int NewEmails { get; set; }
        public int UpdatedEmails { get; set; }
        public DateTime SyncedAt { get; set; }
    }
}
