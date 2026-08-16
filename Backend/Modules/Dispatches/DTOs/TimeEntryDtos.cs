using System;
using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class CreateTimeEntryDto
    {
        // Optional: the specific job (of a multi-job dispatch) this entry is for.
        public int? ServiceOrderJobId { get; set; }
        [Required]
        public string TechnicianId { get; set; } = null!;
        public string? TechnicianName { get; set; }
        [Required]
        public string WorkType { get; set; } = "work";
        [Required]
        public DateTime StartTime { get; set; }
        [Required]
        public DateTime EndTime { get; set; }
        public string? Description { get; set; }
        // Default true: clients that omit the flag (mobile / legacy callers) must keep the
        // pre-existing "everything is billable" behaviour instead of silently losing revenue.
        public bool Billable { get; set; } = true;
        public decimal? HourlyRate { get; set; }

        /// <summary>Required when this entry will push cumulative actuals beyond the planned budget.</summary>
        public string? OverrunReason { get; set; }
    }

    public class UpdateTimeEntryDto
    {
        public string? TechnicianId { get; set; }
        public string? TechnicianName { get; set; }
        public string? WorkType { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Description { get; set; }
        public bool? Billable { get; set; }
    }

    public class TimeEntryDto
    {
        public int Id { get; set; }
        public int DispatchId { get; set; }
        public int? ServiceOrderJobId { get; set; }
        public string TechnicianId { get; set; } = null!;
        public string? TechnicianName { get; set; }
        public string WorkType { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Duration { get; set; }
        public string? Description { get; set; }
        public decimal? TotalCost { get; set; }
        public bool Billable { get; set; }
        public decimal? HourlyRate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? InvoiceStatus { get; set; }
        public string? SourceTable { get; set; } // "service_order" or "dispatch"
        public bool OverrunFlag { get; set; }
        public string? OverrunReason { get; set; }
    }

    public class ApproveTimeEntryDto
    {
        public string ApprovedBy { get; set; } = null!;
        public string? Notes { get; set; }
    }
}
