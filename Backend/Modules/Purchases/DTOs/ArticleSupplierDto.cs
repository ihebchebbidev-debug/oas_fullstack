namespace MyApi.Modules.Purchases.DTOs
{
    public class ArticleSupplierDto
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string? ArticleName { get; set; }
        public string? ArticleNumber { get; set; }
        public int SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierRef { get; set; }
        public decimal PurchasePrice { get; set; }
        public string Currency { get; set; } = "TND";
        public decimal MinOrderQty { get; set; }
        public int LeadTimeDays { get; set; }
        public bool IsPreferred { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
        public List<ArticleSupplierPriceHistoryDto>? PriceHistory { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class ArticleSupplierPriceHistoryDto
    {
        public int Id { get; set; }
        public int ArticleSupplierId { get; set; }
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public string Currency { get; set; } = "TND";
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class CreateArticleSupplierDto
    {
        public int ArticleId { get; set; }
        public int SupplierId { get; set; }
        public string? SupplierRef { get; set; }
        public decimal PurchasePrice { get; set; }
        public string Currency { get; set; } = "TND";
        public decimal MinOrderQty { get; set; } = 1;
        public int LeadTimeDays { get; set; }
        public bool IsPreferred { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateArticleSupplierDto
    {
        public string? SupplierRef { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string? Currency { get; set; }
        public decimal? MinOrderQty { get; set; }
        public int? LeadTimeDays { get; set; }
        public bool? IsPreferred { get; set; }
        public bool? IsActive { get; set; }
        public string? Notes { get; set; }
        public string? PriceChangeReason { get; set; }
    }

    public class PurchaseActivityDto
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string PerformedBy { get; set; } = string.Empty;
        public string? PerformedByName { get; set; }
        public DateTime PerformedAt { get; set; }
    }

    /// <summary>Server-paged cross-entity audit feed (GET /api/purchase-activities).</summary>
    public class PaginatedPurchaseActivityResponse
    {
        public List<PurchaseActivityDto> Activities { get; set; } = new();
        public PurchasePaginationInfo Pagination { get; set; } = new();
    }
}
