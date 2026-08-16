using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Shared.DTOs
{
    public class SystemLogDto
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string Action { get; set; } = "other";
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public object? Metadata { get; set; }
    }

    public class CreateSystemLogRequestDto
    {
        [Required]
        [StringLength(20)]
        public string Level { get; set; } = "info";

        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Module { get; set; } = string.Empty;

        [StringLength(50)]
        public string Action { get; set; } = "other";

        [StringLength(100)]
        public string? UserId { get; set; }

        [StringLength(200)]
        public string? UserName { get; set; }

        [StringLength(100)]
        public string? EntityType { get; set; }

        [StringLength(100)]
        public string? EntityId { get; set; }

        public string? Details { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public object? Metadata { get; set; }
    }

    public class SystemLogSearchRequestDto
    {
        public string? SearchTerm { get; set; }
        public string? Level { get; set; }
        public string? Module { get; set; }
        public string? Action { get; set; }
        public string? UserId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class SystemLogListResponseDto
    {
        public List<SystemLogDto> Logs { get; set; } = new List<SystemLogDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class SystemLogStatisticsDto
    {
        public int TotalLogs { get; set; }
        public int InfoCount { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public int SuccessCount { get; set; }
        public int Last24Hours { get; set; }
        public int Last7Days { get; set; }
    }

    public class CleanupResultDto
    {
        public int DeletedCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
