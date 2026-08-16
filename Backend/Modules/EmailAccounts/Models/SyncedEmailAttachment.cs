using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.EmailAccounts.Models
{
    public class SyncedEmailAttachment : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SyncedEmailId { get; set; }

        [ForeignKey(nameof(SyncedEmailId))]
        public SyncedEmail? SyncedEmail { get; set; }

        [Required]
        [MaxLength(500)]
        public string ExternalAttachmentId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ContentType { get; set; } = "application/octet-stream";

        public long Size { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}