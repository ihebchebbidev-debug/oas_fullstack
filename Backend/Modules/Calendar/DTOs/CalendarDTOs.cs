using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Calendar.DTOs
{
    // Request DTOs
    public class CreateCalendarEventDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public DateTime Start { get; set; }

        [Required]
        public DateTime End { get; set; }

        public bool AllDay { get; set; } = false;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Status { get; set; } = "scheduled";

        [MaxLength(10)]
        public string Priority { get; set; } = "medium";

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(7)]
        public string? Color { get; set; }

        public string? Location { get; set; }
        public string? Attendees { get; set; }
        public string? RelatedType { get; set; }
        public Guid? RelatedId { get; set; }
        public int? ContactId { get; set; }
        public string? Reminders { get; set; }
        public string? Recurring { get; set; }
        public bool IsPrivate { get; set; } = false;

        [Required]
        public Guid CreatedBy { get; set; }
    }

    public class UpdateCalendarEventDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        public string? Description { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public bool? AllDay { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        [MaxLength(20)]
        public string? Status { get; set; }

        [MaxLength(10)]
        public string? Priority { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(7)]
        public string? Color { get; set; }

        public string? Location { get; set; }
        public string? Attendees { get; set; }
        public string? RelatedType { get; set; }
        public Guid? RelatedId { get; set; }
        public int? ContactId { get; set; }
        public string? Reminders { get; set; }
        public string? Recurring { get; set; }
        public bool? IsPrivate { get; set; }
        public Guid? ModifiedBy { get; set; }
    }

    // Response DTOs
    public class CalendarEventDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool AllDay { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Color { get; set; }
        public string? Location { get; set; }
        public string? Attendees { get; set; }
        public string? RelatedType { get; set; }
        public Guid? RelatedId { get; set; }
        public int? ContactId { get; set; }
        public string? Reminders { get; set; }
        public string? Recurring { get; set; }
        public bool IsPrivate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid? ModifiedBy { get; set; }

        // Navigation data
        public string? ContactName { get; set; }
        public string? TypeName { get; set; }
        public List<EventAttendeeDto> EventAttendees { get; set; } = new();
        public List<EventReminderDto> EventReminders { get; set; } = new();
    }

    // Event Type DTOs
    public class CreateEventTypeDto
    {
        [Required]
        [MaxLength(50)]
        public string Id { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [MaxLength(7)]
        public string Color { get; set; } = "#3B82F6";

        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
    }

    public class EventTypeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Color { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Event Attendee DTOs
    public class CreateEventAttendeeDto
    {
        [Required]
        public Guid EventId { get; set; }

        public Guid? UserId { get; set; }

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        public string? Response { get; set; }
    }

    public class EventAttendeeDto
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Response { get; set; }
        public DateTime? RespondedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Event Reminder DTOs
    public class CreateEventReminderDto
    {
        [Required]
        public Guid EventId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = "email";

        [Required]
        public int MinutesBefore { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class EventReminderDto
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public string Type { get; set; } = string.Empty;
        public int MinutesBefore { get; set; }
        public bool IsActive { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
