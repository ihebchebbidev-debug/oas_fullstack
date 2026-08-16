using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.EmailAccounts.Models
{
    public class SyncedCalendarEvent : ITenantEntity
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

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [MaxLength(500)]
        public string? Location { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public bool IsAllDay { get; set; } = false;

        [MaxLength(50)]
        public string Status { get; set; } = "confirmed";

        [MaxLength(255)]
        public string? OrganizerEmail { get; set; }

        public string? Attendees { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}