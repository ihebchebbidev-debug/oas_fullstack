using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.WebsiteBuilder.DTOs
{
    // ══════════════════════════════════════════════════════════════
    // SITE DTOs
    // ══════════════════════════════════════════════════════════════

    public class WBSiteResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Favicon { get; set; }
        public string ThemeJson { get; set; } = "{}";
        public bool Published { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? PublishedUrl { get; set; }
        public string? DefaultLanguage { get; set; }
        public string? LanguagesJson { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public List<WBPageResponseDto> Pages { get; set; } = new List<WBPageResponseDto>();
    }

    public class WBSiteListResponseDto
    {
        public List<WBSiteResponseDto> Sites { get; set; } = new List<WBSiteResponseDto>();
        public int TotalCount { get; set; }
    }

    public class CreateWBSiteRequestDto
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public string? ThemeJson { get; set; }

        [StringLength(10)]
        public string? DefaultLanguage { get; set; } = "en";

        /// <summary>
        /// Optional: pre-built pages (from templates). If null, a default Home page is created.
        /// </summary>
        public List<CreateWBPageRequestDto>? Pages { get; set; }
    }

    public class UpdateWBSiteRequestDto
    {
        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(100)]
        public string? Slug { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(2000)]
        public string? Favicon { get; set; }

        public string? ThemeJson { get; set; }

        public bool? Published { get; set; }

        [StringLength(10)]
        public string? DefaultLanguage { get; set; }

        public string? LanguagesJson { get; set; }
    }

    public class WBSiteSearchRequestDto
    {
        public string? SearchTerm { get; set; }
        public bool? Published { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "UpdatedAt";
        public string? SortDirection { get; set; } = "desc";
    }

    // ══════════════════════════════════════════════════════════════
    // PAGE DTOs
    // ══════════════════════════════════════════════════════════════

    public class WBPageResponseDto
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string ComponentsJson { get; set; } = "[]";
        public string SeoJson { get; set; } = "{}";
        public string? TranslationsJson { get; set; }
        public bool IsHomePage { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        /// <summary>Wave 2: timestamp the page was last published (snapshot frozen).</summary>
        public DateTime? PublishedAt { get; set; }
    }

    public class CreateWBPageRequestDto
    {
        [Required]
        public int SiteId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Slug { get; set; }

        public string? ComponentsJson { get; set; }

        public string? SeoJson { get; set; }

        public bool IsHomePage { get; set; } = false;

        public int SortOrder { get; set; } = 0;
    }

    public class UpdateWBPageRequestDto
    {
        [StringLength(200)]
        public string? Title { get; set; }

        [StringLength(100)]
        public string? Slug { get; set; }

        public string? ComponentsJson { get; set; }

        public string? SeoJson { get; set; }

        public string? TranslationsJson { get; set; }

        public bool? IsHomePage { get; set; }

        public int? SortOrder { get; set; }

        /// <summary>
        /// Wave 2 — optimistic concurrency token. If set, the server compares
        /// against the row's current UpdatedAt; mismatch → 409 Conflict.
        /// Optional for backward compatibility.
        /// </summary>
        public DateTime? ExpectedUpdatedAt { get; set; }
    }

    public class UpdateWBPageComponentsRequestDto
    {
        [Required]
        public string ComponentsJson { get; set; } = "[]";

        /// <summary>
        /// If set, updates the translation for this language instead of the base components.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Wave 2 — optimistic concurrency token. Client sends the `UpdatedAt`
        /// it last received for the page. If the DB row has been updated since
        /// (different `UpdatedAt`), the server returns 409 Conflict instead of
        /// silently overwriting another tab's changes. Optional for backward
        /// compatibility — clients that omit it get the legacy last-writer-wins
        /// behaviour.
        /// </summary>
        public DateTime? ExpectedUpdatedAt { get; set; }
    }

    /// <summary>
    /// Wave 2 — returned by the components-update endpoint so the client can
    /// refresh its concurrency token (UpdatedAt) for the next save.
    /// </summary>
    public class WBPageSaveResultDto
    {
        public int PageId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ReorderWBPagesRequestDto
    {
        [Required]
        public int SiteId { get; set; }

        [Required]
        public List<int> PageIds { get; set; } = new List<int>();
    }

    // ══════════════════════════════════════════════════════════════
    // PAGE VERSION DTOs
    // ══════════════════════════════════════════════════════════════

    public class WBPageVersionResponseDto
    {
        public int Id { get; set; }
        public int PageId { get; set; }
        public int SiteId { get; set; }
        public int VersionNumber { get; set; }
        public string ComponentsJson { get; set; } = "[]";
        public string? ChangeMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class CreateWBPageVersionRequestDto
    {
        [StringLength(500)]
        public string? ChangeMessage { get; set; }
    }

    // ══════════════════════════════════════════════════════════════
    // GLOBAL BLOCK DTOs
    // ══════════════════════════════════════════════════════════════

    public class WBGlobalBlockResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ComponentJson { get; set; } = "{}";
        public string? Category { get; set; }
        public string? Tags { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public int UsageCount { get; set; }
    }

    public class CreateWBGlobalBlockRequestDto
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string ComponentJson { get; set; } = "{}";

        [StringLength(100)]
        public string? Category { get; set; }

        public string? Tags { get; set; }
    }

    public class UpdateWBGlobalBlockRequestDto
    {
        [StringLength(200)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? ComponentJson { get; set; }

        [StringLength(100)]
        public string? Category { get; set; }

        public string? Tags { get; set; }
    }

    // ══════════════════════════════════════════════════════════════
    // BRAND PROFILE DTOs
    // ══════════════════════════════════════════════════════════════

    public class WBBrandProfileResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ThemeJson { get; set; } = "{}";
        public bool IsBuiltIn { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class CreateWBBrandProfileRequestDto
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string ThemeJson { get; set; } = "{}";
    }

    public class UpdateWBBrandProfileRequestDto
    {
        [StringLength(200)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? ThemeJson { get; set; }
    }

    // ══════════════════════════════════════════════════════════════
    // FORM SUBMISSION DTOs
    // ══════════════════════════════════════════════════════════════

    public class WBFormSubmissionResponseDto
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public int? PageId { get; set; }
        public string FormComponentId { get; set; } = string.Empty;
        public string FormLabel { get; set; } = string.Empty;
        public string PageTitle { get; set; } = string.Empty;
        public string DataJson { get; set; } = "{}";
        public string? Source { get; set; }
        public string? WebhookStatus { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class WBFormSubmissionListResponseDto
    {
        public List<WBFormSubmissionResponseDto> Submissions { get; set; } = new List<WBFormSubmissionResponseDto>();
        public int TotalCount { get; set; }
    }

    public class CreateWBFormSubmissionRequestDto
    {
        [Required]
        public int SiteId { get; set; }

        public int? PageId { get; set; }

        [Required]
        [StringLength(100)]
        public string FormComponentId { get; set; } = string.Empty;

        [StringLength(200)]
        public string FormLabel { get; set; } = string.Empty;

        [StringLength(200)]
        public string PageTitle { get; set; } = string.Empty;

        [Required]
        public string DataJson { get; set; } = "{}";

        [StringLength(50)]
        public string? Source { get; set; } = "website";
    }

    // ══════════════════════════════════════════════════════════════
    // MEDIA DTOs
    // ══════════════════════════════════════════════════════════════

    public class WBMediaResponseDto
    {
        public int Id { get; set; }
        public int? SiteId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? Folder { get; set; }
        public string? AltText { get; set; }
        public DateTime UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
    }

    public class CreateWBMediaRequestDto
    {
        public int? SiteId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string OriginalName { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string FileUrl { get; set; } = string.Empty;

        public long FileSize { get; set; } = 0;

        [StringLength(100)]
        public string ContentType { get; set; } = "image/jpeg";

        public int? Width { get; set; }

        public int? Height { get; set; }

        [StringLength(200)]
        public string? Folder { get; set; }

        [StringLength(500)]
        public string? AltText { get; set; }
    }

    // ══════════════════════════════════════════════════════════════
    // TEMPLATE DTOs
    // ══════════════════════════════════════════════════════════════

    public class WBTemplateResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = "general";
        public string? PreviewImageUrl { get; set; }
        public string ThemeJson { get; set; } = "{}";
        public string PagesJson { get; set; } = "[]";
        public string? Tags { get; set; }
        public bool IsPremium { get; set; }
        public bool IsBuiltIn { get; set; }
        public int SortOrder { get; set; }
    }

    public class CreateWBTemplateRequestDto
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = "general";

        [StringLength(2000)]
        public string? PreviewImageUrl { get; set; }

        [Required]
        public string ThemeJson { get; set; } = "{}";

        [Required]
        public string PagesJson { get; set; } = "[]";

        public string? Tags { get; set; }

        public bool IsPremium { get; set; } = false;

        public int SortOrder { get; set; } = 0;
    }

    // ══════════════════════════════════════════════════════════════
    // ACTIVITY LOG DTOs
    // ══════════════════════════════════════════════════════════════

    public class WBActivityLogResponseDto
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public int? PageId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
