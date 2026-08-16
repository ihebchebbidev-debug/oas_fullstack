using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Projects.DTOs
{
    // Response DTOs - matching actual database schema
    public class ProjectTaskResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TaskType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public DateTime? DueDate { get; set; }
        public int? AssignedUserId { get; set; }
        public string? AssignedUserName { get; set; }
        public string? AssignedUserProfilePictureUrl { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class DailyTaskResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TaskType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public DateTime? DueDate { get; set; }
        public int? AssignedUserId { get; set; }
        public string? AssignedUserName { get; set; }
        public string? AssignedUserProfilePictureUrl { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class TaskListResponseDto
    {
        public List<ProjectTaskResponseDto> ProjectTasks { get; set; } = new List<ProjectTaskResponseDto>();
        public List<DailyTaskResponseDto> DailyTasks { get; set; } = new List<DailyTaskResponseDto>();
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    // Request DTOs
    public class CreateProjectTaskRequestDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [StringLength(50)]
        public string TaskType { get; set; } = "follow-up";

        [StringLength(50)]
        public string Status { get; set; } = "open";

        [StringLength(50)]
        public string? RelatedEntityType { get; set; }

        public int? RelatedEntityId { get; set; }

        public DateTime? DueDate { get; set; }

        public int? AssignedUserId { get; set; }
    }

    public class CreateDailyTaskRequestDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [StringLength(50)]
        public string TaskType { get; set; } = "follow-up";

        [StringLength(50)]
        public string Status { get; set; } = "open";

        [StringLength(50)]
        public string? RelatedEntityType { get; set; }

        public int? RelatedEntityId { get; set; }

        public DateTime? DueDate { get; set; }

        public int? AssignedUserId { get; set; }
    }

    public class UpdateProjectTaskRequestDto
    {
        [StringLength(200)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        [StringLength(50)]
        public string? TaskType { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }

        [StringLength(50)]
        public string? RelatedEntityType { get; set; }

        public int? RelatedEntityId { get; set; }

        public DateTime? DueDate { get; set; }

        public int? AssignedUserId { get; set; }
    }

    public class UpdateDailyTaskRequestDto
    {
        [StringLength(200)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        [StringLength(50)]
        public string? TaskType { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }

        [StringLength(50)]
        public string? RelatedEntityType { get; set; }

        public int? RelatedEntityId { get; set; }

        public DateTime? DueDate { get; set; }

        public int? AssignedUserId { get; set; }
        
        [StringLength(20)]
        public string? Priority { get; set; }

    }

    public class MoveTaskRequestDto
    {
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;
    }

    public class BulkMoveTasksRequestDto
    {
        public List<TaskMoveDto> Tasks { get; set; } = new List<TaskMoveDto>();
    }

    public class TaskMoveDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;
    }

    public class TaskSearchRequestDto
    {
        public string? SearchTerm { get; set; }
        public string? TaskType { get; set; }
        public string? Status { get; set; }
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public int? AssignedUserId { get; set; }
        public DateTime? DueDateFrom { get; set; }
        public DateTime? DueDateTo { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "CreatedDate";
        public string? SortDirection { get; set; } = "desc";
    }

    public class AssignTaskRequestDto
    {
        public int? AssignedUserId { get; set; }
    }

    public class BulkAssignTasksRequestDto
    {
        public List<int> TaskIds { get; set; } = new List<int>();
        public int? AssignedUserId { get; set; }
    }

    public class BulkUpdateTaskStatusDto
    {
        public List<int> TaskIds { get; set; } = new List<int>();
        
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;
    }

    public class TaskStatisticsDto
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int TasksDueToday { get; set; }
        public int TasksDueThisWeek { get; set; }
        public Dictionary<string, int> TasksByType { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> TasksByStatus { get; set; } = new Dictionary<string, int>();
    }

    public class ProjectTaskStatsDto
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public double CompletionPercentage { get; set; }
    }
}
