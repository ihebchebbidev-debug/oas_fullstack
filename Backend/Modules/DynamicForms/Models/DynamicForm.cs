using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.Shared.Domain.Common;

namespace MyApi.Modules.DynamicForms.Models
{
    /// <summary>
    /// Status workflow for dynamic forms
    /// </summary>
    public enum FormStatus
    {
        Draft,
        Released,
        Archived
    }

    /// <summary>
    /// Field types available in the form builder
    /// </summary>
    public enum FieldType
    {
        Text,
        Textarea,
        Number,
        Checkbox,
        Radio,
        Select,
        Date,
        Section,
        Email,
        Phone,
        Signature,
        Rating,
        PageBreak,  // For multi-page forms
        Content     // Rich content block (titles, text, links, buttons)
    }

    /// <summary>
    /// Dynamic Form entity - stores form definitions with fields as JSON
    /// </summary>
    [ModuleScope("dynamic_forms")]
    [Table("DynamicForms")]
    public class DynamicForm : BaseEntityWithStatus, ITenantEntity
    {
        public int TenantId { get; set; }
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

        [Required]
        public FormStatus Status { get; set; } = FormStatus.Draft;

        [Required]
        public int Version { get; set; } = 1;

        [MaxLength(100)]
        public string? Category { get; set; }

        /// <summary>
        /// Whether the form is publicly accessible without authentication
        /// </summary>
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// URL-friendly slug for public access (e.g., "customer-satisfaction-survey")
        /// </summary>
        [MaxLength(200)]
        public string? PublicSlug { get; set; }

        /// <summary>
        /// Optional cut-off date - public submissions are refused after it
        /// </summary>
        public DateTime? ClosesAt { get; set; }

        /// <summary>
        /// Optional cap on the number of accepted submissions
        /// </summary>
        public int? MaxResponses { get; set; }

        /// <summary>
        /// JSON blob containing field definitions
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string Fields { get; set; } = "[]";

        /// <summary>
        /// JSON blob containing thank you page settings (messages, rules, redirects)
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string? ThankYouSettings { get; set; }

        [MaxLength(100)]
        public string? CreatedUser { get; set; }

        [MaxLength(100)]
        public string? ModifyUser { get; set; }

        // Navigation property for responses
        public virtual ICollection<DynamicFormResponse> Responses { get; set; } = new List<DynamicFormResponse>();
    }

    /// <summary>
    /// Dynamic Form Response - stores submitted form data
    /// </summary>
    [Table("DynamicFormResponses")]
    public class DynamicFormResponse : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        public int FormId { get; set; }

        [Required]
        public int FormVersion { get; set; }

        /// <summary>
        /// Type of entity this response is linked to (e.g., "service_order", "installation")
        /// </summary>
        [MaxLength(50)]
        public string? EntityType { get; set; }

        /// <summary>
        /// ID of the linked entity
        /// </summary>
        [MaxLength(100)]
        public string? EntityId { get; set; }

        /// <summary>
        /// JSON blob containing field responses (field_id -> value)
        /// </summary>
        [Required]
        [Column(TypeName = "jsonb")]
        public string Responses { get; set; } = "{}";

        [MaxLength(2000)]
        public string? Notes { get; set; }

        /// <summary>
        /// Name of the submitter (for public submissions)
        /// </summary>
        [MaxLength(200)]
        public string? SubmitterName { get; set; }

        /// <summary>
        /// Email of the submitter (for public submissions)
        /// </summary>
        [MaxLength(200)]
        public string? SubmitterEmail { get; set; }

        /// <summary>
        /// Whether this was a public submission
        /// </summary>
        public bool IsPublicSubmission { get; set; } = false;

        [Required]
        [MaxLength(100)]
        public string SubmittedBy { get; set; } = string.Empty;

        [Required]
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("FormId")]
        public virtual DynamicForm? Form { get; set; }
    }

    /// <summary>
    /// Condition operators for conditional logic
    /// </summary>
    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        Contains,
        NotContains,
        GreaterThan,
        LessThan,
        IsEmpty,
        IsNotEmpty
    }

    /// <summary>
    /// Condition action type
    /// </summary>
    public enum ConditionAction
    {
        Show,
        Hide
    }

    /// <summary>
    /// Field condition for conditional visibility
    /// </summary>
    public class FieldCondition
    {
        public string FieldId { get; set; } = string.Empty;
        public ConditionOperator Operator { get; set; }
        public object? Value { get; set; }
    }

    /// <summary>
    /// Field definition structure (used within Fields JSON)
    /// </summary>
    public class FormFieldDefinition
    {
        public string Id { get; set; } = string.Empty;
        public FieldType Type { get; set; }
        public string LabelEn { get; set; } = string.Empty;
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
        public List<FieldOption>? Options { get; set; }
        
        // Dynamic data source configuration
        public bool UseDynamicData { get; set; } = false;
        public DynamicDataSource? DataSource { get; set; }
        
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public bool? Collapsible { get; set; }
        public int? MaxStars { get; set; }
        // Conditional logic
        public FieldCondition? Condition { get; set; }
        public ConditionAction? ConditionAction { get; set; }
    }

    /// <summary>
    /// Option for radio/select/checkbox fields (static options)
    /// </summary>
    public class FieldOption
    {
        public string Id { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string LabelEn { get; set; } = string.Empty;
        public string LabelFr { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dynamic data source entity types
    /// </summary>
    public enum DynamicDataEntityType
    {
        Contacts,
        Articles,
        Materials,
        Services,
        Offers,
        Sales,
        Installations,
        Users
    }

    /// <summary>
    /// Filter operators for dynamic data queries
    /// </summary>
    public enum DataFilterOperator
    {
        Equals,
        NotEquals,
        Contains,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        IsEmpty,
        IsNotEmpty
    }

    /// <summary>
    /// Filter condition for dynamic data
    /// </summary>
    public class DataFilter
    {
        public string Field { get; set; } = string.Empty;
        public DataFilterOperator Operator { get; set; }
        public object? Value { get; set; }
    }

    /// <summary>
    /// Configuration for dynamic data source
    /// </summary>
    public class DynamicDataSource
    {
        public DynamicDataEntityType EntityType { get; set; }
        public string DisplayField { get; set; } = string.Empty;
        public string ValueField { get; set; } = string.Empty;
        public string? DisplayTemplate { get; set; }
        public List<DataFilter>? Filters { get; set; }
        public string? SortField { get; set; }
        public string? SortOrder { get; set; } // "asc" or "desc"
        public int? Limit { get; set; }
    }

    /// <summary>
    /// Thank You page default message settings
    /// </summary>
    public class ThankYouDefaultMessage
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
    /// Thank You page conditional rule
    /// </summary>
    public class ThankYouRule
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public FieldCondition Condition { get; set; } = new();
        public string? TitleEn { get; set; }
        public string? TitleFr { get; set; }
        public string MessageEn { get; set; } = string.Empty;
        public string MessageFr { get; set; } = string.Empty;
        public string? RedirectUrl { get; set; }
        public int RedirectDelay { get; set; } = 3;
        public int Priority { get; set; } = 1;
    }

    /// <summary>
    /// Thank You page settings structure (stored as JSON in ThankYouSettings column)
    /// </summary>
    public class ThankYouSettingsDefinition
    {
        public ThankYouDefaultMessage DefaultMessage { get; set; } = new();
        public List<ThankYouRule>? Rules { get; set; }
    }
}
