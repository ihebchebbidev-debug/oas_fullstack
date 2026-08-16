namespace MyApi.Modules.EmailAccounts.DTOs
{
    public class SyncedCalendarEventDto
    {
        public Guid Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Location { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsAllDay { get; set; }
        public string Status { get; set; } = "confirmed";
        public string? OrganizerEmail { get; set; }
        public string? Attendees { get; set; } // JSON array
    }

    public class SyncedCalendarEventsPageDto
    {
        public List<SyncedCalendarEventDto> Events { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class CalendarSyncResultDto
    {
        public int NewEvents { get; set; }
        public int UpdatedEvents { get; set; }
        public DateTime SyncedAt { get; set; }
    }
}
