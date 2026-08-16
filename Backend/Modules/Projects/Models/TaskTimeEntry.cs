using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Projects.Models
{
    /// <summary>
    /// TaskTimeEntry model for tracking time spent on tasks
    /// Supports both Project Tasks and Daily Tasks
    /// </summary>
    [ModuleScope("projects")]
    public class TaskTimeEntry : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to ProjectTask (nullable - one of ProjectTaskId or DailyTaskId must be set)
        /// </summary>
        public int? ProjectTaskId { get; set; }

        /// <summary>
        /// Foreign key to DailyTask (nullable - one of ProjectTaskId or DailyTaskId must be set)
        /// </summary>
        public int? DailyTaskId { get; set; }

        /// <summary>
        /// User who logged this time entry
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Name of the user (denormalized for display)
        /// </summary>
        [StringLength(100)]
        public string? UserName { get; set; }

        /// <summary>
        /// Start time of the work
        /// </summary>
        [Required]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time of the work
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Duration in minutes
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Duration { get; set; }

        /// <summary>
        /// Description/notes about the work performed
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether this time entry is billable
        /// </summary>
        public bool IsBillable { get; set; } = true;

        /// <summary>
        /// Hourly rate for billing (if applicable)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? HourlyRate { get; set; }

        /// <summary>
        /// Total cost calculated from duration and hourly rate
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalCost { get; set; }

        /// <summary>
        /// Work type: work, break, meeting, review, other
        /// </summary>
        [StringLength(50)]
        public string WorkType { get; set; } = "work";

        /// <summary>
        /// Approval status: pending, approved, rejected
        /// </summary>
        [StringLength(20)]
        public string ApprovalStatus { get; set; } = "pending";

        /// <summary>
        /// User who approved/rejected this entry
        /// </summary>
        public int? ApprovedById { get; set; }

        /// <summary>
        /// Date when this entry was approved/rejected
        /// </summary>
        public DateTime? ApprovedDate { get; set; }

        /// <summary>
        /// Notes from the approver
        /// </summary>
        public string? ApprovalNotes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        // Navigation properties
        [ForeignKey("ProjectTaskId")]
        public virtual ProjectTask? ProjectTask { get; set; }

        [ForeignKey("DailyTaskId")]
        public virtual DailyTask? DailyTask { get; set; }

        [ForeignKey("UserId")]
        public virtual MyApi.Modules.Users.Models.User? User { get; set; }

        [ForeignKey("ApprovedById")]
        public virtual MyApi.Modules.Users.Models.User? ApprovedBy { get; set; }
    }
}
