using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.EmailAccounts.Models
{
    public class SyncedEmail : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ConnectedEmailAccountId { get; set; }

        [ForeignKey(nameof(ConnectedEmailAccountId))]
        public ConnectedEmailAccount? ConnectedEmailAccount { get; set; }

        [Required]
        [MaxLength(255)]
        public string ExternalId { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? ThreadId { get; set; }

        public string Subject { get; set; } = string.Empty;

        public string? Snippet { get; set; }

        [MaxLength(255)]
        public string FromEmail { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? FromName { get; set; }

        public string? ToEmails { get; set; }
        public string? CcEmails { get; set; }
        public string? BccEmails { get; set; }
        public string? BodyPreview { get; set; }

        public bool IsRead { get; set; } = false;
        public bool IsStarred { get; set; } = false;
        public bool HasAttachments { get; set; } = false;

        public string? Labels { get; set; }

        public DateTime ReceivedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<SyncedEmailAttachment> Attachments { get; set; } = new List<SyncedEmailAttachment>();
    }
}