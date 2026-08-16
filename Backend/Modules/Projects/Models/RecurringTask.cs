using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Projects.Models
{
    [ModuleScope("projects")]
    public class RecurringTask : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        // Source task (template)
        public int? ProjectTaskId { get; set; }
        public int? DailyTaskId { get; set; }

        [Required]
        [MaxLength(50)]
        public string RecurrenceType { get; set; } = "daily"; // daily, weekly, monthly, yearly, custom

        // For weekly: comma-separated days (0=Sunday, 1=Monday, etc.)
        [MaxLength(50)]
        public string? DaysOfWeek { get; set; }

        // For monthly: day of month (1-31) or -1 for last day
        public int? DayOfMonth { get; set; }

        // For yearly: month and day
        public int? MonthOfYear { get; set; }

        // Custom interval (e.g., every 2 days, every 3 weeks)
        public int Interval { get; set; } = 1;

        // Recurrence boundaries
        [Required]
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MaxOccurrences { get; set; }
        public int OccurrenceCount { get; set; } = 0;

        // Next scheduled generation
        public DateTime? NextOccurrence { get; set; }
        public DateTime? LastGeneratedDate { get; set; }

        // Status
        public bool IsActive { get; set; } = true;
        public bool IsPaused { get; set; } = false;

        // Audit
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        // Navigation properties
        [ForeignKey("ProjectTaskId")]
        public virtual ProjectTask? ProjectTask { get; set; }

        [ForeignKey("DailyTaskId")]
        public virtual DailyTask? DailyTask { get; set; }
    }

    [ModuleScope("projects")]
    public class RecurringTaskLog : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        public int RecurringTaskId { get; set; }

        // The generated task
        public int? GeneratedProjectTaskId { get; set; }
        public int? GeneratedDailyTaskId { get; set; }

        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        public DateTime ScheduledFor { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "created"; // created, skipped, failed

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation
        [ForeignKey("RecurringTaskId")]
        public virtual RecurringTask? RecurringTask { get; set; }
    }
}
