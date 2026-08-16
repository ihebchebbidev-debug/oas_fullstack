using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Calendar.Models
{
    [ModuleScope("calendar")]
    [Table("event_reminders")]
    public class EventReminder : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        public Guid Id { get; set; }

        [Required]
        [Column("event_id")]
        public Guid EventId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = "email";

        [Required]
        [Column("minutes_before")]
        public int MinutesBefore { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("sent_at")]
        public DateTime? SentAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual CalendarEvent CalendarEvent { get; set; } = null!;
    }
}