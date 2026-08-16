namespace MyApi.Modules.Projects.DTOs
{
    public class CreateRecurringTaskDto
    {
        public int? ProjectTaskId { get; set; }
        public int? DailyTaskId { get; set; }
        public string RecurrenceType { get; set; } = "daily";
        public string? DaysOfWeek { get; set; }
        public int? DayOfMonth { get; set; }
        public int? MonthOfYear { get; set; }
        public int Interval { get; set; } = 1;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MaxOccurrences { get; set; }
    }

    public class UpdateRecurringTaskDto
    {
        public string? RecurrenceType { get; set; }
        public string? DaysOfWeek { get; set; }
        public int? DayOfMonth { get; set; }
        public int? MonthOfYear { get; set; }
        public int? Interval { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MaxOccurrences { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsPaused { get; set; }
    }

    public class RecurringTaskResponseDto
    {
        public int Id { get; set; }
        public int? ProjectTaskId { get; set; }
        public int? DailyTaskId { get; set; }
        public string RecurrenceType { get; set; } = string.Empty;
        public string? DaysOfWeek { get; set; }
        public int? DayOfMonth { get; set; }
        public int? MonthOfYear { get; set; }
        public int Interval { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MaxOccurrences { get; set; }
        public int OccurrenceCount { get; set; }
        public DateTime? NextOccurrence { get; set; }
        public DateTime? LastGeneratedDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsPaused { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;

        // Display helpers
        public string RecurrenceDescription { get; set; } = string.Empty;
        public string? SourceTaskTitle { get; set; }
    }

    public class RecurringTaskLogResponseDto
    {
        public int Id { get; set; }
        public int RecurringTaskId { get; set; }
        public int? GeneratedProjectTaskId { get; set; }
        public int? GeneratedDailyTaskId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public DateTime ScheduledFor { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class GenerateRecurringTasksDto
    {
        public DateTime? UpToDate { get; set; }
        public bool DryRun { get; set; } = false;
    }

    public class GenerateResultDto
    {
        public int TotalProcessed { get; set; }
        public int TasksGenerated { get; set; }
        public int TasksSkipped { get; set; }
        public int TasksFailed { get; set; }
        public List<string> Messages { get; set; } = new();
    }
}
