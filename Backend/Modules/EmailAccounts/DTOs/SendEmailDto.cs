namespace MyApi.Modules.EmailAccounts.DTOs
{
    public class SendEmailDto
    {
        public List<string> To { get; set; } = new();
        public List<string> Cc { get; set; } = new();
        public List<string> Bcc { get; set; } = new();
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public string? BodyHtml { get; set; }
        public List<EmailAttachmentDto>? Attachments { get; set; }
    }

    public class EmailAttachmentDto
    {
        /// <summary>File name including extension, e.g. "quote-123.pdf"</summary>
        public string FileName { get; set; } = "";
        /// <summary>MIME type, e.g. "application/pdf"</summary>
        public string ContentType { get; set; } = "application/octet-stream";
        /// <summary>Base64-encoded file content</summary>
        public string ContentBase64 { get; set; } = "";
    }

    public class SendEmailResultDto
    {
        public bool Success { get; set; }
        public string? MessageId { get; set; }
        public string? Error { get; set; }
    }
}
