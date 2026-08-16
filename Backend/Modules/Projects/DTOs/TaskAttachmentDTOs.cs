using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Projects.DTOs
{
    // Response DTOs - matches database schema
    public class TaskAttachmentResponseDto
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public bool IsImage { get; set; }
        public bool IsDocument { get; set; }
    }

    public class TaskAttachmentListResponseDto
    {
        public List<TaskAttachmentResponseDto> Attachments { get; set; } = new List<TaskAttachmentResponseDto>();
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public long TotalSize { get; set; }
        public string TotalSizeFormatted { get; set; } = string.Empty;
    }

    // Request DTOs
    public class CreateTaskAttachmentRequestDto
    {
        [Required]
        public int TaskId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string UploadedBy { get; set; } = string.Empty;
    }

    public class UpdateTaskAttachmentRequestDto
    {
        [StringLength(255)]
        public string? FileName { get; set; }
    }

    public class TaskAttachmentSearchRequestDto
    {
        public int? TaskId { get; set; }
        public string? UploadedBy { get; set; }
        public string? SearchTerm { get; set; }
        public string? ContentType { get; set; }
        public bool? IsImage { get; set; }
        public bool? IsDocument { get; set; }
        public DateTime? UploadedFrom { get; set; }
        public DateTime? UploadedTo { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "UploadedDate";
        public string? SortDirection { get; set; } = "desc";
    }

    public class BulkDeleteTaskAttachmentsDto
    {
        public List<int> AttachmentIds { get; set; } = new List<int>();
    }
}
