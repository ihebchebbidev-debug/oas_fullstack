using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Modules.Contacts.Models;
using MyApi.Infrastructure;

namespace MyApi.Modules.Calendar.Models
{
    [ModuleScope("calendar")]
    [Table("calendar_events")]
    public class CalendarEvent : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public DateTime Start { get; set; }

        [Required]
        public DateTime End { get; set; }

        [Column("all_day")]
        public bool AllDay { get; set; } = false;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "scheduled";

        [Required]
        [MaxLength(10)]
        public string Priority { get; set; } = "medium";

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(7)]
        public string? Color { get; set; }

        public string? Location { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Attendees { get; set; }

        [MaxLength(20)]
        [Column("related_type")]
        public string? RelatedType { get; set; }

        [Column("related_id")]
        public Guid? RelatedId { get; set; }

        [Column("contact_id")]
        public int? ContactId { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Reminders { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Recurring { get; set; }

        [Column("is_private")]
        public bool IsPrivate { get; set; } = false;

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("created_by")]
        public Guid CreatedBy { get; set; }

        [Column("modified_by")]
        public Guid? ModifiedBy { get; set; }

        // Navigation properties
        public virtual Contact? Contact { get; set; }
        public virtual EventType? EventTypeNavigation { get; set; }
        public virtual ICollection<EventAttendee> EventAttendees { get; set; } = new List<EventAttendee>();
        public virtual ICollection<EventReminder> EventReminders { get; set; } = new List<EventReminder>();
    }
}