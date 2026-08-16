namespace MyApi.Modules.ExternalEndpoints.DTOs
{
    public class ExternalEndpointDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string AllowedMethods { get; set; } = "POST";
        public string? AllowedOrigins { get; set; }
        public string? ExpectedSchema { get; set; }
        public string? ResponseTemplate { get; set; }
        public string? WebhookForwardUrl { get; set; }
        /// <summary>Days to retain inbound logs (0 = forever). Pruned by background sweep.</summary>
        public int LogRetentionDays { get; set; }
        /// <summary>Comma-separated accepted content types: "json", "xml", "form", "any".</summary>
        public string AcceptedContentTypes { get; set; } = "any";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public int TotalReceived { get; set; }
        public int ReceivedToday { get; set; }
        public DateTime? LastReceived { get; set; }
        public int TotalSent { get; set; }
        public int SentToday { get; set; }
    }

    public class CreateExternalEndpointDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public string AllowedMethods { get; set; } = "POST";
        public string? AllowedOrigins { get; set; }
        public string? ExpectedSchema { get; set; }
        public string? ResponseTemplate { get; set; }
        public string? WebhookForwardUrl { get; set; }
        public int LogRetentionDays { get; set; } = 30;
        public string AcceptedContentTypes { get; set; } = "any";
    }

    public class UpdateExternalEndpointDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public string? AllowedMethods { get; set; }
        public string? AllowedOrigins { get; set; }
        public string? ExpectedSchema { get; set; }
        public string? ResponseTemplate { get; set; }
        public string? WebhookForwardUrl { get; set; }
        public int? LogRetentionDays { get; set; }
        public string? AcceptedContentTypes { get; set; }
    }

    /// <summary>Preview of how an inbound log payload maps onto offer/sale fields.
    /// Field names mirror the frontend create-form contract so the UI can
    /// pre-fill without an additional mapping layer. The <see cref="Confidence"/>
    /// dictionary tells the UI whether each value was extracted verbatim from
    /// a known key ("exact"), derived/guessed ("inferred"), or absent ("none").
    /// </summary>
    public class ConvertLogPreviewDto
    {
        public int LogId { get; set; }
        public string? ContactName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Currency { get; set; }
        public string? Notes { get; set; }
        public decimal? TotalAmount { get; set; }
        public List<ConvertLogItemDto> Items { get; set; } = new();
        /// <summary>Per-field confidence: "exact" | "inferred" | "none".</summary>
        public Dictionary<string, string> Confidence { get; set; } = new();
        /// <summary>Raw parsed JSON object for the UI to inspect.</summary>
        public object? RawJson { get; set; }
    }

    public class ConvertLogItemDto
    {
        public string? Description { get; set; }
        public decimal? Quantity { get; set; } = 1;
        public decimal? UnitPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        /// <summary>"exact" if all of description+qty+price were direct keys; "inferred" otherwise.</summary>
        public string Confidence { get; set; } = "inferred";
    }

    public class ExternalEndpointLogDto
    {
        public int Id { get; set; }
        public int EndpointId { get; set; }
        public string Method { get; set; } = string.Empty;
        public string? Headers { get; set; }
        public string? QueryString { get; set; }
        public string? Body { get; set; }
        public string? SourceIp { get; set; }
        public int StatusCode { get; set; }
        public string? ResponseBody { get; set; }
        public DateTime ReceivedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public bool IsRead { get; set; }
        /// <summary>MIME type of the inbound body (e.g. "application/json", "application/xml").</summary>
        public string? ContentType { get; set; }
        /// <summary>Company / tenant ID supplied by the sender for multi-company ERP setups.</summary>
        public string? CompanyId { get; set; }
    }

    public class ExternalEndpointStatsDto
    {
        public int TotalEndpoints { get; set; }
        public int ActiveEndpoints { get; set; }
        public int TotalReceivedToday { get; set; }
        public int TotalReceivedAll { get; set; }
        public int TotalSentToday { get; set; }
        public int TotalSentAll { get; set; }
    }

    public class PaginatedEndpointResponse
    {
        public List<ExternalEndpointDto> Endpoints { get; set; } = new();
        public PaginationInfo Pagination { get; set; } = new();
    }

    public class PaginatedLogResponse
    {
        public List<ExternalEndpointLogDto> Logs { get; set; } = new();
        public PaginationInfo Pagination { get; set; } = new();
    }

    public class PaginationInfo
    {
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
    }
}
