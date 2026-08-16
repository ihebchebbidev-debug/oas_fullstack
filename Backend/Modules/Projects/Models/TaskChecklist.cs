using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Projects.Models
{
    /// <summary>
    /// TaskChecklist model for grouping checklist items within a task
    /// </summary>
    [ModuleScope("projects")]
    public class TaskChecklist : ITenantEntity
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
        /// Checklist title
        /// </summary>
        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Optional description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether the checklist is expanded in the UI
        /// </summary>
        public bool IsExpanded { get; set; } = true;

        /// <summary>
        /// Sort order for multiple checklists
        /// </summary>
        public int SortOrder { get; set; }

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

        public virtual ICollection<TaskChecklistItem> Items { get; set; } = new List<TaskChecklistItem>();
    }

    /// <summary>
    /// TaskChecklistItem model for individual items within a checklist
    /// </summary>
    [ModuleScope("projects")]
    public class TaskChecklistItem : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to parent TaskChecklist
        /// </summary>
        [Required]
        public int ChecklistId { get; set; }

        /// <summary>
        /// Item title/description
        /// </summary>
        [Required]
        [StringLength(500)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Whether this item is completed
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// When this item was completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// User who completed this item
        /// </summary>
        public int? CompletedById { get; set; }

        /// <summary>
        /// Name of user who completed (denormalized)
        /// </summary>
        [StringLength(100)]
        public string? CompletedByName { get; set; }

        /// <summary>
        /// Sort order within the checklist
        /// </summary>
        public int SortOrder { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        // Navigation properties
        [ForeignKey("ChecklistId")]
        public virtual TaskChecklist Checklist { get; set; } = null!;

        [ForeignKey("CompletedById")]
        public virtual MyApi.Modules.Users.Models.User? CompletedByUser { get; set; }
    }
}
