using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Projects.DTOs
{
    // Response DTOs
    public class ProjectResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ContactId { get; set; }
        public string? ContactName { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ProjectKind { get; set; } = "client";
        public string? Priority { get; set; }
        public decimal? Budget { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public ProjectSettingsDto? Settings { get; set; }

        // Team members as user IDs
        public List<int> TeamMembers { get; set; } = new List<int>();

        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public List<ProjectColumnDto> Columns { get; set; } = new List<ProjectColumnDto>();
    }

    public class ProjectColumnDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public string? Color { get; set; }
    }

    public class ProjectListResponseDto
    {
        public List<ProjectResponseDto> Projects { get; set; } = new List<ProjectResponseDto>();
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    public class ProjectStatisticsDto
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int OnHoldProjects { get; set; }
        public int HighPriorityCount { get; set; }
        public int MediumPriorityCount { get; set; }
        public int LowPriorityCount { get; set; }
    }

    public class BulkUpdateProjectStatusRequestDto
    {
        public List<int> ProjectIds { get; set; } = new List<int>();
        public string Status { get; set; } = "active";
    }

    public class BulkArchiveProjectsRequestDto
    {
        public List<int> ProjectIds { get; set; } = new List<int>();
        public bool Archive { get; set; } = true;
    }

    // Request DTOs
    public class CreateProjectRequestDto
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? ContactId { get; set; }

        // Team members as user IDs
        public List<int>? TeamMembers { get; set; }

        [StringLength(20)]
        public string? Status { get; set; } = "active";

        [StringLength(20)]
        public string? ProjectKind { get; set; } = "client";

        [StringLength(20)]
        public string? Priority { get; set; } = "medium";

        public decimal? Budget { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? LinkOfferId { get; set; }
        public int? LinkSaleId { get; set; }
        public int? LinkServiceOrderId { get; set; }
        public int? LinkDispatchId { get; set; }

        public bool CreateDefaultColumns { get; set; } = true;
    }

    public class UpdateProjectRequestDto
    {
        [StringLength(200)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        public int? ContactId { get; set; }

        // Team members as user IDs
        public List<int>? TeamMembers { get; set; }

        [StringLength(20)]
        public string? Status { get; set; }

        [StringLength(20)]
        public string? ProjectKind { get; set; }

        [StringLength(20)]
        public string? Priority { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? LinkOfferId { get; set; }
        public int? LinkSaleId { get; set; }
        public int? LinkServiceOrderId { get; set; }
        public int? LinkDispatchId { get; set; }
    }

    public class ProjectLinkedEntityDto
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Status { get; set; }
        public DateTime? Date { get; set; }
        /// <summary>For sales: true when the sale is a won-deal of this project.</summary>
        public bool? IsDeal { get; set; }
        public decimal? Amount { get; set; }
    }

    public class ProjectLinksDto
    {
        public int ProjectId { get; set; }
        public List<ProjectLinkedEntityDto> Offers { get; set; } = new();
        public List<ProjectLinkedEntityDto> Sales { get; set; } = new();
        public List<ProjectLinkedEntityDto> ServiceOrders { get; set; } = new();
        public List<ProjectLinkedEntityDto> Dispatches { get; set; } = new();
    }

    public class LinkProjectEntityRequestDto
    {
        [Required]
        public string EntityType { get; set; } = string.Empty;
        [Required]
        public int EntityId { get; set; }
    }

    public class AssignTeamMemberRequestDto
    {
        [Required]
        public int UserId { get; set; }
        public string? UserName { get; set; }
    }

    public class RemoveTeamMemberRequestDto
    {
        [Required]
        public int UserId { get; set; }
    }

    public class ProjectSettingsDto
    {
        public bool AutoLinkConvertedEntities { get; set; } = true;
        public bool RequireProjectBeforeConvertingOffer { get; set; }
        public string DefaultTaskStatus { get; set; } = "todo";
        public bool AllowCrossProjectDispatch { get; set; } = true;
        public bool ShowFinancialDataInProjectTabs { get; set; } = true;
        public string DefaultLinkedEntityType { get; set; } = "service_order";
    }

    public class ProjectNoteDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class CreateProjectNoteRequestDto
    {
        [Required]
        [StringLength(5000)]
        public string Content { get; set; } = string.Empty;
    }

    public class UpdateProjectNoteRequestDto
    {
        [StringLength(5000)]
        public string? Content { get; set; }
    }

    public class ProjectActivityDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }
    }

    public class ProjectSearchRequestDto
    {
        public string? SearchTerm { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public int? ContactId { get; set; }
        public DateTime? StartDateFrom { get; set; }
        public DateTime? StartDateTo { get; set; }
        public DateTime? EndDateFrom { get; set; }
        public DateTime? EndDateTo { get; set; }
        /// <summary>false = hide archived projects, true = only archived. Null = no filter.</summary>
        public bool? IsArchived { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "CreatedDate";
        public string? SortDirection { get; set; } = "desc";
    }
}
