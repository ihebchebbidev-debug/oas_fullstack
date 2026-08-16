using System;
using System.Collections.Generic;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class UserLightDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
    }

    public class SchedulingDto
    {
        public DateTime? ScheduledDate { get; set; }
        public TimeSpan? ScheduledStartTime { get; set; }
        public TimeSpan? ScheduledEndTime { get; set; }
        public int? EstimatedDuration { get; set; }
        public int? TravelTime { get; set; }
        public decimal? TravelDistance { get; set; }
    }

    public class DispatchListItemDto
    {
        public int Id { get; set; }
        public string DispatchNumber { get; set; } = null!;
        public int? JobId { get; set; }
        public int? ServiceOrderId { get; set; }
        public int? ProjectId { get; set; }
        public int? ContactId { get; set; }
        public string? ContactName { get; set; }
        public string? SiteAddress { get; set; }
        public string Status { get; set; } = null!;
        public string Priority { get; set; } = null!;
        public List<UserLightDto> AssignedTechnicians { get; set; } = new();
        public List<string>? RequiredSkills { get; set; }
        public SchedulingDto? Scheduling { get; set; }
        // Added for technician filtering - contains [TECH_ID:xxx] marker
        public string? Notes { get; set; }
        public string? DispatchedBy { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public string? ScheduledStartTime { get; set; }
        public string? ScheduledEndTime { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        // Multi-job / installation dispatch fields
        public int? InstallationId { get; set; }
        public string? InstallationName { get; set; }
        public List<int> JobIds { get; set; } = new();
        // Job summaries for multi-job dispatches (used by the planning board hover).
        public List<DispatchJobSummaryDto> Jobs { get; set; } = new();
    }

    public class DispatchJobSummaryDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public int? EstimatedDuration { get; set; }
        public string? Priority { get; set; }
    }

    public class DispatchDto : DispatchListItemDto
    {
        public List<object> TimeEntries { get; set; } = new();
        public List<object> Expenses { get; set; } = new();
        public List<object> MaterialsUsed { get; set; } = new();
        public List<object> Attachments { get; set; } = new();
        public new List<object> Notes { get; set; } = new();
        public int CompletionPercentage { get; set; }
        public new string? DispatchedBy { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
