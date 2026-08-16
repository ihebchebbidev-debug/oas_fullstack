using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Projects.DTOs
{
    // Response DTOs - matches TaskComments table: Id, TaskId, Comment, CreatedDate, CreatedBy
    public class TaskCommentResponseDto
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class TaskCommentListResponseDto
    {
        public List<TaskCommentResponseDto> Comments { get; set; } = new List<TaskCommentResponseDto>();
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    // Request DTOs
    public class CreateTaskCommentRequestDto
    {
        [Required]
        public int TaskId { get; set; }

        [Required]
        public string Comment { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class UpdateTaskCommentRequestDto
    {
        [Required]
        public string Comment { get; set; } = string.Empty;
    }

    public class TaskCommentSearchRequestDto
    {
        public int? TaskId { get; set; }
        public string? CreatedBy { get; set; }
        public string? SearchTerm { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "CreatedDate";
        public string? SortDirection { get; set; } = "desc";
    }
}
