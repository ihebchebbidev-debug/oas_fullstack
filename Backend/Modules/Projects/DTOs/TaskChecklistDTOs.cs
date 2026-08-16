using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Projects.DTOs
{
    /// <summary>
    /// Response DTO for task checklists
    /// </summary>
    public class TaskChecklistResponseDto
    {
        public int Id { get; set; }
        public int? ProjectTaskId { get; set; }
        public int? DailyTaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsExpanded { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public List<TaskChecklistItemResponseDto> Items { get; set; } = new();
        
        // Computed properties
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
        public int ProgressPercent { get; set; }
    }

    /// <summary>
    /// Response DTO for checklist items
    /// </summary>
    public class TaskChecklistItemResponseDto
    {
        public int Id { get; set; }
        public int ChecklistId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedById { get; set; }
        public string? CompletedByName { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
    }

    /// <summary>
    /// Request DTO for creating a new checklist
    /// </summary>
    public class CreateTaskChecklistDto
    {
        public int? ProjectTaskId { get; set; }
        public int? DailyTaskId { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    /// <summary>
    /// Request DTO for updating a checklist
    /// </summary>
    public class UpdateTaskChecklistDto
    {
        [StringLength(255)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        public bool? IsExpanded { get; set; }

        public int? SortOrder { get; set; }
    }

    /// <summary>
    /// Request DTO for creating a checklist item
    /// </summary>
    public class CreateChecklistItemDto
    {
        [Required]
        public int ChecklistId { get; set; }

        [Required]
        [StringLength(500)]
        public string Title { get; set; } = string.Empty;

        public int? SortOrder { get; set; }
    }

    /// <summary>
    /// Request DTO for updating a checklist item
    /// </summary>
    public class UpdateChecklistItemDto
    {
        [StringLength(500)]
        public string? Title { get; set; }

        public bool? IsCompleted { get; set; }

        public int? SortOrder { get; set; }
    }

    /// <summary>
    /// Request DTO for reordering checklist items
    /// </summary>
    public class ReorderChecklistItemsDto
    {
        [Required]
        public int ChecklistId { get; set; }

        [Required]
        public List<int> ItemIds { get; set; } = new();
    }

    /// <summary>
    /// Request DTO for bulk creating checklist items
    /// </summary>
    public class BulkCreateChecklistItemsDto
    {
        [Required]
        public int ChecklistId { get; set; }

        [Required]
        public List<string> Titles { get; set; } = new();
    }
}
