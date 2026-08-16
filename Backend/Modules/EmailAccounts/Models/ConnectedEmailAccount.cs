using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.EmailAccounts.Models
{
    /// <summary>
    /// Represents a connected email/calendar account (Gmail or Outlook)
    /// </summary>
    public class ConnectedEmailAccount : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public int UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Handle { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Provider { get; set; } = string.Empty;

        [Required]
        public string AccessToken { get; set; } = string.Empty;

        [Required]
        public string RefreshToken { get; set; } = string.Empty;

        public string? Scopes { get; set; }

        [MaxLength(50)]
        public string SyncStatus { get; set; } = "not_synced";

        public DateTime? LastSyncedAt { get; set; }

        public DateTime? AuthFailedAt { get; set; }

        [MaxLength(50)]
        public string EmailVisibility { get; set; } = "share_everything";

        [MaxLength(50)]
        public string CalendarVisibility { get; set; } = "share_everything";

        [MaxLength(50)]
        public string ContactAutoCreationPolicy { get; set; } = "sent_and_received";

        public bool IsEmailSyncEnabled { get; set; } = true;

        public bool IsCalendarSyncEnabled { get; set; } = true;

        public bool ExcludeGroupEmails { get; set; } = false;

        public bool ExcludeNonProfessionalEmails { get; set; } = false;

        public bool IsCalendarContactAutoCreationEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<EmailBlocklistItem> BlocklistItems { get; set; } = new List<EmailBlocklistItem>();
    }
}