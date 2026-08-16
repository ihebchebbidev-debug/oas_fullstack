namespace MyApi.Modules.EmailAccounts.DTOs
{
    /// <summary>
    /// DTO to create a calendar event on an external provider (Google Calendar / Outlook Calendar)
    /// </summary>
    public class CreateExternalCalendarEventDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Location { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsAllDay { get; set; } = false;
        public List<string>? Attendees { get; set; } // List of email addresses
    }

    public class CreateExternalCalendarEventResultDto
    {
        public bool Success { get; set; }
        public string? ExternalId { get; set; }
        public string? Error { get; set; }
    }
}
