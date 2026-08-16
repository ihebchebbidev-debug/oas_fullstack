using System.Collections.Generic;

namespace MyApi.Modules.EmailAccounts.DTOs
{
    public class CustomEmailConfigDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? DisplayName { get; set; }

        // IMAP
        public string? ImapServer { get; set; }
        public int? ImapPort { get; set; }
        public string? ImapSecurity { get; set; }

        // POP3
        public string? Pop3Server { get; set; }
        public int? Pop3Port { get; set; }
        public string? Pop3Security { get; set; }

        // SMTP
        public string? SmtpServer { get; set; }
        public int? SmtpPort { get; set; }
        public string? SmtpSecurity { get; set; }
    }

    public class SendCustomEmailDto
    {
        public CustomEmailConfigDto Config { get; set; } = new CustomEmailConfigDto();
        public List<string> To { get; set; } = new List<string>();
        public List<string>? Cc { get; set; }
        public List<string>? Bcc { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? BodyHtml { get; set; }
    }

    public class FetchCustomEmailsDto
    {
        public CustomEmailConfigDto Config { get; set; } = new CustomEmailConfigDto();
        public string Folder { get; set; } = "INBOX";
        public int Limit { get; set; } = 50;
    }

    // Persistence DTOs
    public class CreateCustomEmailAccountDto
    {
        public CustomEmailConfigDto Config { get; set; } = new CustomEmailConfigDto();
        public string? DisplayName { get; set; }
    }

    public class CustomEmailAccountDto
    {
        public Guid Id { get; set; }
        public int? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? SmtpServer { get; set; }
        public int? SmtpPort { get; set; }
        public string? SmtpSecurity { get; set; }
        public string? ImapServer { get; set; }
        public int? ImapPort { get; set; }
        public string? ImapSecurity { get; set; }
        public string? Pop3Server { get; set; }
        public int? Pop3Port { get; set; }
        public string? Pop3Security { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? ConnectedAccountId { get; set; }
    }
}
