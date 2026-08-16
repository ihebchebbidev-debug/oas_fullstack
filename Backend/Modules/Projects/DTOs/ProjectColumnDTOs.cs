using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Projects.DTOs
{
    // Response DTOs - matches ProjectColumns table: Id, ProjectId, Name, DisplayOrder, Color
    public class ProjectColumnResponseDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public string? Color { get; set; }
        public int TaskCount { get; set; }
    }

    public class ProjectColumnListResponseDto
    {
        public List<ProjectColumnResponseDto> Columns { get; set; } = new List<ProjectColumnResponseDto>();
        public int TotalCount { get; set; }
    }

    // Request DTOs
    public class CreateProjectColumnRequestDto
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int DisplayOrder { get; set; }

        [StringLength(7)]
        public string? Color { get; set; }
    }

    public class UpdateProjectColumnRequestDto
    {
        [StringLength(100)]
        public string? Name { get; set; }

        public int? DisplayOrder { get; set; }

        [StringLength(7)]
        public string? Color { get; set; }
    }

    public class ReorderProjectColumnsRequestDto
    {
        public List<ProjectColumnPositionDto> Columns { get; set; } = new List<ProjectColumnPositionDto>();
    }

    public class ProjectColumnPositionDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int DisplayOrder { get; set; }
    }

    public class BulkDeleteProjectColumnsDto
    {
        public List<int> ColumnIds { get; set; } = new List<int>();
        public int? MoveTasksToColumnId { get; set; }
    }
}
