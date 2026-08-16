using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Projects.DTOs
{
    /// <summary>
    /// Response DTO for task time entries
    /// </summary>
    public class TaskTimeEntryResponseDto
    {
        public int Id { get; set; }
        public int? ProjectTaskId { get; set; }
        public string? ProjectTaskTitle { get; set; }
        public int? DailyTaskId { get; set; }
        public string? DailyTaskTitle { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal Duration { get; set; } // in minutes
        public string? Description { get; set; }
        public bool IsBillable { get; set; }
        public decimal? HourlyRate { get; set; }
        public decimal? TotalCost { get; set; }
        public string WorkType { get; set; } = "work";
        public string ApprovalStatus { get; set; } = "pending";
        public int? ApprovedById { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string? ApprovalNotes { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
    }

    /// <summary>
    /// Request DTO for creating a new time entry
    /// </summary>
    public class CreateTaskTimeEntryDto
    {
        /// <summary>
        /// Project task ID (required if DailyTaskId is not provided)
        /// </summary>
        public int? ProjectTaskId { get; set; }

        /// <summary>
        /// Daily task ID (required if ProjectTaskId is not provided)
        /// </summary>
        public int? DailyTaskId { get; set; }

        /// <summary>
        /// User logging the time (defaults to current user)
        /// </summary>
        public int? UserId { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Duration in minutes (calculated if StartTime and EndTime provided)
        /// </summary>
        public decimal? Duration { get; set; }

        public string? Description { get; set; }

        public bool IsBillable { get; set; } = true;

        public decimal? HourlyRate { get; set; }

        [StringLength(50)]
        public string? WorkType { get; set; } = "work";
    }

    /// <summary>
    /// Request DTO for updating a time entry
    /// </summary>
    public class UpdateTaskTimeEntryDto
    {
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal? Duration { get; set; }
        public string? Description { get; set; }
        public bool? IsBillable { get; set; }
        public decimal? HourlyRate { get; set; }

        [StringLength(50)]
        public string? WorkType { get; set; }
    }

    /// <summary>
    /// Request DTO for approving/rejecting a time entry
    /// </summary>
    public class ApproveTaskTimeEntryDto
    {
        [Required]
        public bool IsApproved { get; set; }

        public string? Notes { get; set; }
    }

    /// <summary>
    /// Summary DTO for task time tracking statistics
    /// </summary>
    public class TaskTimeTrackingSummaryDto
    {
        public int TaskId { get; set; }
        public string TaskType { get; set; } = "project"; // "project" or "daily"
        public string TaskTitle { get; set; } = string.Empty;
        public decimal TotalLoggedMinutes { get; set; }
        public decimal TotalLoggedHours => TotalLoggedMinutes / 60m;
        public decimal? EstimatedHours { get; set; }
        public decimal? RemainingEstimate { get; set; }
        public decimal? TotalBillableMinutes { get; set; }
        public decimal? TotalCost { get; set; }
        public int EntryCount { get; set; }
        public List<TaskTimeEntryResponseDto> Entries { get; set; } = new List<TaskTimeEntryResponseDto>();
    }

    /// <summary>
    /// Request DTO for querying time entries
    /// </summary>
    public class TaskTimeEntryQueryDto
    {
        public int? ProjectTaskId { get; set; }
        public int? DailyTaskId { get; set; }
        public int? UserId { get; set; }
        public int? ProjectId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool? IsBillable { get; set; }
        public string? ApprovalStatus { get; set; }
        public string? WorkType { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "CreatedDate";
        public string SortDirection { get; set; } = "desc";
    }

    /// <summary>
    /// DTO for bulk time entry operations
    /// </summary>
    public class BulkApproveTimeEntriesDto
    {
        [Required]
        public List<int> TimeEntryIds { get; set; } = new List<int>();

        [Required]
        public bool IsApproved { get; set; }

        public string? Notes { get; set; }
    }
}
