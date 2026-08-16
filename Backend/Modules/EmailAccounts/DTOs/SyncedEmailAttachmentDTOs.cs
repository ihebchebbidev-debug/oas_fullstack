namespace MyApi.Modules.EmailAccounts.DTOs
{
    /// <summary>
    /// Attachment metadata returned with synced emails
    /// </summary>
    public class SyncedEmailAttachmentDto
    {
        public Guid Id { get; set; }
        public string ExternalAttachmentId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long Size { get; set; }
    }

    /// <summary>
    /// Response when downloading an attachment (base64 content)
    /// </summary>
    public class AttachmentDownloadDto
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long Size { get; set; }
        public string ContentBase64 { get; set; } = string.Empty;
    }
}
