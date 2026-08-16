namespace MyApi.Modules.Deals.DTOs
{
    public class DealContactSummaryDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Company { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
    }

    public class DealItemDto
    {
        public int Id { get; set; }
        public int DealId { get; set; }
        public int? ArticleId { get; set; }
        public string Type { get; set; } = "article";
        public string ItemName { get; set; } = string.Empty;
        public string? ItemCode { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public string DiscountType { get; set; } = "percentage";
        public decimal LineTotal { get; set; }
        public int DisplayOrder { get; set; }
        public string? InstallationId { get; set; }
        public string? InstallationName { get; set; }
    }

    public class DealActivityDto
    {
        public int Id { get; set; }
        public int DealId { get; set; }
        public string Type { get; set; } = "note";
        public string Description { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
    }

    public class DealDto
    {
        public int Id { get; set; }
        public string? DealNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ContactId { get; set; }
        public string? ContactName { get; set; }
        public DealContactSummaryDto? Contact { get; set; }
        public int? ProjectId { get; set; }
        public string Stage { get; set; } = "lead";
        public int Probability { get; set; }
        public decimal EstimatedValue { get; set; }
        public string Currency { get; set; } = "TND";
        public DateTime? ExpectedCloseDate { get; set; }
        public DateTime? ActualCloseDate { get; set; }
        public DateTime? NextActionDate { get; set; }
        public string? NextAction { get; set; }
        public string? LostReason { get; set; }
        public string? Category { get; set; }
        public string? Source { get; set; }
        public string? Notes { get; set; }
        public string[]? Tags { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        public string? ConvertedToOfferId { get; set; }
        public string? ConvertedToSaleId { get; set; }
        public string? ConvertedToProjectId { get; set; }
        public DateTime? ConvertedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime? LastActivity { get; set; }
        public List<DealItemDto> Items { get; set; } = new();
    }

    public class CreateDealItemDto
    {
        public int? ArticleId { get; set; }
        public string Type { get; set; } = "article";
        public string ItemName { get; set; } = string.Empty;
        public string? ItemCode { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; } = 0;
        public decimal Discount { get; set; } = 0;
        public string DiscountType { get; set; } = "percentage";
        public string? InstallationId { get; set; }
        public string? InstallationName { get; set; }
    }

    public class CreateDealDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ContactId { get; set; }
        public int? ProjectId { get; set; }
        public string Stage { get; set; } = "lead";
        public int Probability { get; set; } = 0;
        public decimal EstimatedValue { get; set; } = 0;
        public string Currency { get; set; } = "TND";
        public DateTime? ExpectedCloseDate { get; set; }
        public DateTime? NextActionDate { get; set; }
        public string? NextAction { get; set; }
        public string? Category { get; set; }
        public string? Source { get; set; }
        public string? Notes { get; set; }
        public string[]? Tags { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        public List<CreateDealItemDto>? Items { get; set; }
    }

    public class UpdateDealDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? ContactId { get; set; }
        public int? ProjectId { get; set; }
        public string? Stage { get; set; }
        public int? Probability { get; set; }
        public decimal? EstimatedValue { get; set; }
        public string? Currency { get; set; }
        public DateTime? ExpectedCloseDate { get; set; }
        public DateTime? ActualCloseDate { get; set; }
        public DateTime? NextActionDate { get; set; }
        public string? NextAction { get; set; }
        public string? LostReason { get; set; }
        public string? Category { get; set; }
        public string? Source { get; set; }
        public string? Notes { get; set; }
        public string[]? Tags { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        /// <summary>When non-null, the deal's line items are replaced with this exact set
        /// in a single transaction (an empty list clears them). Leave null to keep items as-is.</summary>
        public List<CreateDealItemDto>? Items { get; set; }
    }

    /// <summary>Convert a deal into a Sale and/or a Project (and/or an Offer).</summary>
    public class ConvertDealDto
    {
        public bool ConvertToSale { get; set; }
        public bool ConvertToProject { get; set; }
        public bool ConvertToOffer { get; set; }
    }

    public class ConvertDealResultDto
    {
        public int? SaleId { get; set; }
        public int? ProjectId { get; set; }
        public int? OfferId { get; set; }
    }

    public class CreateDealActivityDto
    {
        public string Type { get; set; } = "note";
        public string Description { get; set; } = string.Empty;
        public string? Details { get; set; }
    }

    public class DealStatsDto
    {
        public long TotalDeals { get; set; }
        public long OpenDeals { get; set; }
        public long WonDeals { get; set; }
        public long LostDeals { get; set; }
        public decimal TotalValue { get; set; }
        public decimal OpenValue { get; set; }
        public decimal WonValue { get; set; }
        public decimal AverageValue { get; set; }
        public decimal WinRate { get; set; }
    }

    public class PaginationInfoDto
    {
        public int Page { get; set; }
        public int Limit { get; set; }
        public long Total { get; set; }
        public int TotalPages { get; set; }
    }

    public class PaginatedDealResponse
    {
        public List<DealDto> Deals { get; set; } = new();
        public PaginationInfoDto Pagination { get; set; } = new();
    }
}
