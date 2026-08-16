using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.DynamicForms.DTOs
{
    /// <summary>
    /// Generic paged envelope returned by list endpoints
    /// </summary>
    public class PagedResultDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; }
    }

    /// <summary>
    /// DTO for reading dynamic form data
    /// </summary>
    public class DynamicFormDto
    {
        public int Id { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string NameFr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionFr { get; set; }
        public string Status { get; set; } = "draft";
        public int Version { get; set; }
        public string? Category { get; set; }
        public bool IsPublic { get; set; }
        public string? PublicSlug { get; set; }
        public string? PublicUrl { get; set; }
        public List<FormFieldDto> Fields { get; set; } = new();
        public ThankYouSettingsDto? ThankYouSettings { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ResponseCount { get; set; }

        /// <summary>
        /// Optional date after which the public form stops accepting submissions
        /// </summary>
        public DateTime? ClosesAt { get; set; }

        /// <summary>
        /// Optional maximum number of accepted submissions
        /// </summary>
        public int? MaxResponses { get; set; }
    }

    /// <summary>
    /// DTO for creating a new dynamic form
    /// </summary>
    public class CreateDynamicFormDto
    {
        [Required]
        [MaxLength(200)]
        public string NameEn { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string NameFr { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? DescriptionEn { get; set; }

        [MaxLength(1000)]
        public string? DescriptionFr { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        public List<FormFieldDto> Fields { get; set; } = new();
    }

    /// <summary>
    /// DTO for updating an existing dynamic form
    /// </summary>
    public class UpdateDynamicFormDto
    {
        [MaxLength(200)]
        public string? NameEn { get; set; }

        [MaxLength(200)]
        public string? NameFr { get; set; }

        [MaxLength(1000)]
        public string? DescriptionEn { get; set; }

        [MaxLength(1000)]
        public string? DescriptionFr { get; set; }

        public string? Status { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        public bool? IsPublic { get; set; }

        [MaxLength(200)]
        public string? PublicSlug { get; set; }

        public List<FormFieldDto>? Fields { get; set; }

        public ThankYouSettingsDto? ThankYouSettings { get; set; }

        public DateTime? ClosesAt { get; set; }

        /// <summary>
        /// Explicitly clear the close date when true
        /// </summary>
        public bool? ClearClosesAt { get; set; }

        public int? MaxResponses { get; set; }

        /// <summary>
        /// Explicitly clear the max-responses cap when true
        /// </summary>
        public bool? ClearMaxResponses { get; set; }

        /// <summary>
        /// Optimistic concurrency guard - the version the client loaded.
        /// When it no longer matches the stored version the update is rejected.
        /// </summary>
        public int? ExpectedVersion { get; set; }
    }

    /// <summary>
    /// DTO for public form submission (unauthenticated)
    /// </summary>
    public class PublicSubmitFormResponseDto
    {
        [Required]
        public Dictionary<string, object> Responses { get; set; } = new();

        [MaxLength(200)]
        public string? SubmitterName { get; set; }

        [MaxLength(200)]
        [EmailAddress]
        public string? SubmitterEmail { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO for cascading/dependent field configuration
    /// </summary>
    public class FieldDependencyDto
    {
        public string ParentFieldId { get; set; } = string.Empty;
        public string ParentValueField { get; set; } = string.Empty;
        public string FilterField { get; set; } = string.Empty;
        public bool ClearOnParentChange { get; set; } = true;
    }

    /// <summary>
    /// DTO for field condition
    /// </summary>
    public class FieldConditionDto
    {
        public string FieldId { get; set; } = string.Empty;
        public string Operator { get; set; } = "equals";
        public object? Value { get; set; }
    }

    /// <summary>
    /// DTO for form field definition
    /// </summary>
    public class FormFieldDto
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = "text";

        [Required]
        public string LabelEn { get; set; } = string.Empty;

        [Required]
        public string LabelFr { get; set; } = string.Empty;

        public string? DescriptionEn { get; set; }
        public string? DescriptionFr { get; set; }
        public string? PlaceholderEn { get; set; }
        public string? PlaceholderFr { get; set; }
        
        // Hint text - additional helper text shown below the field
        public string? HintEn { get; set; }
        public string? HintFr { get; set; }
        
        // Link/button configuration
        public string? LinkUrl { get; set; }
        public string? LinkTextEn { get; set; }
        public string? LinkTextFr { get; set; }
        public string? LinkStyle { get; set; } // "link" or "button"
        public bool? LinkNewTab { get; set; }
        
        public bool Required { get; set; }
        public int Order { get; set; }
        public string? Width { get; set; } // "full", "half", or "third"
        
        // Static options for radio/select/checkbox
        public List<FieldOptionDto>? Options { get; set; }
        
        // Dynamic data source configuration
        public bool UseDynamicData { get; set; } = false;
        public DynamicDataSourceDto? DataSource { get; set; }
        
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public bool? Collapsible { get; set; }
        public int? MaxStars { get; set; }
        // Conditional logic
        public FieldConditionDto? Condition { get; set; }
        public string? ConditionAction { get; set; }

        // Cascading/dependent dropdown configuration
        public FieldDependencyDto? Dependency { get; set; }
    }

    /// <summary>
    /// DTO for field option (static options)
    /// </summary>
    public class FieldOptionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string LabelEn { get; set; } = string.Empty;
        public string LabelFr { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for data filter condition
    /// </summary>
    public class DataFilterDto
    {
        public string Field { get; set; } = string.Empty;
        public string Operator { get; set; } = "equals";
        public object? Value { get; set; }
    }

    /// <summary>
    /// DTO for dynamic data source configuration
    /// </summary>
    public class DynamicDataSourceDto
    {
        /// <summary>
        /// Entity type: contacts, articles, materials, services, offers, sales, installations, users
        /// </summary>
        public string EntityType { get; set; } = string.Empty;
        
        /// <summary>
        /// Field to display as option label (e.g., "name", "email")
        /// </summary>
        public string DisplayField { get; set; } = string.Empty;
        
        /// <summary>
        /// Field to store as value (e.g., "id")
        /// </summary>
        public string ValueField { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional template for display (e.g., "{name} - {email}")
        /// </summary>
        public string? DisplayTemplate { get; set; }
        
        /// <summary>
        /// Optional filters to narrow down data
        /// </summary>
        public List<DataFilterDto>? Filters { get; set; }
        
        /// <summary>
        /// Field to sort by
        /// </summary>
        public string? SortField { get; set; }
        
        /// <summary>
        /// Sort order: "asc" or "desc"
        /// </summary>
        public string? SortOrder { get; set; }
        
        /// <summary>
        /// Maximum number of options to show
        /// </summary>
        public int? Limit { get; set; }
    }

    /// <summary>
    /// DTO for form response/submission
    /// </summary>
    public class DynamicFormResponseDto
    {
        public int Id { get; set; }
        public int FormId { get; set; }
        public int FormVersion { get; set; }
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public Dictionary<string, object> Responses { get; set; } = new();
        public string? Notes { get; set; }
        public string? SubmitterName { get; set; }
        public string? SubmitterEmail { get; set; }
        public bool IsPublicSubmission { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
    }

    /// <summary>
    /// DTO for submitting a form response
    /// </summary>
    public class SubmitFormResponseDto
    {
        [Required]
        public int FormId { get; set; }

        public string? EntityType { get; set; }
        public string? EntityId { get; set; }

        [Required]
        public Dictionary<string, object> Responses { get; set; } = new();

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO for changing form status
    /// </summary>
    public class ChangeFormStatusDto
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Query parameters for listing forms
    /// </summary>
    public class DynamicFormQueryParams
    {
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Search { get; set; }
        /// <summary>
        /// 1-based page index. When null (or PageSize is null) no pagination is applied.
        /// </summary>
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public string SortBy { get; set; } = "UpdatedAt";
        public bool SortDesc { get; set; } = true;
    }

    /// <summary>
    /// DTO for thank you page default message
    /// </summary>
    public class ThankYouDefaultMessageDto
    {
        public string? TitleEn { get; set; }
        public string? TitleFr { get; set; }
        public string? MessageEn { get; set; }
        public string? MessageFr { get; set; }
        public bool EnableRedirect { get; set; } = false;
        public string? RedirectUrl { get; set; }
        public int RedirectDelay { get; set; } = 3;
    }

    /// <summary>
    /// DTO for thank you page conditional rule
    /// </summary>
    public class ThankYouRuleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public FieldConditionDto Condition { get; set; } = new();
        public string? TitleEn { get; set; }
        public string? TitleFr { get; set; }
        public string MessageEn { get; set; } = string.Empty;
        public string MessageFr { get; set; } = string.Empty;
        public string? RedirectUrl { get; set; }
        public int RedirectDelay { get; set; } = 3;
        public int Priority { get; set; } = 1;
    }

    /// <summary>
    /// DTO for thank you page settings
    /// </summary>
    public class ThankYouSettingsDto
    {
        public ThankYouDefaultMessageDto DefaultMessage { get; set; } = new();
        public List<ThankYouRuleDto>? Rules { get; set; }
    }
}
