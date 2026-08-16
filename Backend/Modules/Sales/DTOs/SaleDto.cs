namespace MyApi.Modules.Sales.DTOs
{
    public class SaleDto
    {
        public int Id { get; set; }
        public string? SaleNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        
        // Contact Information
        public int ContactId { get; set; }
        public int? ProjectId { get; set; }
        /// <summary>True when this sale is a won "deal" of a Project (converted from a project offer).</summary>
        public bool IsDeal { get; set; }
        public ContactSummaryDto? Contact { get; set; }
        
        // Financial Information
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TND";
        public decimal? Taxes { get; set; }
        public string? TaxType { get; set; }
        public decimal? Discount { get; set; }
        /// <summary>"percentage" or "fixed" — how <see cref="Discount"/> should be read.</summary>
        public string? DiscountType { get; set; }
        public decimal? FiscalStamp { get; set; }
        public decimal? TotalAmount { get; set; }
        
        // Status & Classification
        public string Status { get; set; } = "created";
        public string? Stage { get; set; }
        public string? Priority { get; set; }
        public string? Category { get; set; }
        public string? Source { get; set; }
        
        // Addresses
        public string? BillingAddress { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryPostalCode { get; set; }
        public string? DeliveryCountry { get; set; }
        
        // Dates
        public DateTime? EstimatedCloseDate { get; set; }
        public DateTime? ActualCloseDate { get; set; }
        public DateTime? ValidUntil { get; set; }
        
        // Assignment
        public string? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        
        // Tags
        public string[]? Tags { get; set; }
        
        // Items
        public List<SaleItemDto>? Items { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime? LastActivity { get; set; }
        
        // Link to original offer
        public string? OfferId { get; set; }

        public DateTime? ConvertedFromOfferAt { get; set; }
        public string? LostReason { get; set; }
        public string? MaterialsFulfillment { get; set; }
        public string? ServiceOrdersStatus { get; set; }
        
        // Computed field: the first service order ID created from this sale's items
        public string? ConvertedToServiceOrderId { get; set; }
        
        public string? Notes { get; set; }
    }

    public class ContactSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Company { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        
        // Geolocation fields
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int HasLocation { get; set; } = 0;
    }

    public class SaleItemDto
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public string Type { get; set; } = string.Empty;
        public int? ArticleId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? ItemCode { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal Discount { get; set; }
        public string DiscountType { get; set; } = "percentage";
        public string? InstallationId { get; set; }
        public string? InstallationName { get; set; }

        public bool RequiresServiceOrder { get; set; }
        public bool ServiceOrderGenerated { get; set; }
        public string? ServiceOrderId { get; set; }
        public string? FulfillmentStatus { get; set; }
        public int DisplayOrder { get; set; }
        // Currency this line was priced in. When null, the parent Sale.Currency applies.
        public string? Currency { get; set; }
    }

    public class CreateSaleDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ContactId { get; set; }
        public int? ProjectId { get; set; }
        public string Status { get; set; } = "created";
        public string? Stage { get; set; } = "closed";
        public string? Priority { get; set; }
        public string? Category { get; set; }
        public string? Source { get; set; }
        public string Currency { get; set; } = "TND";
        public DateTime? EstimatedCloseDate { get; set; }
        public DateTime? ActualCloseDate { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? BillingAddress { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryPostalCode { get; set; }
        public string? DeliveryCountry { get; set; }
        public decimal? Taxes { get; set; }
        public string? TaxType { get; set; }
        public decimal? Discount { get; set; }
        public string? DiscountType { get; set; }
        public decimal? FiscalStamp { get; set; }
        public string? OfferId { get; set; }
        public List<CreateSaleItemDto>? Items { get; set; }
    }

    public class CreateSaleItemDto
    {
        public string Type { get; set; } = string.Empty;
        public int? ArticleId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? ItemCode { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; } = 0;
        public string DiscountType { get; set; } = "percentage";
        public string? InstallationId { get; set; }
        public string? InstallationName { get; set; }

        public bool RequiresServiceOrder { get; set; } = false;
        public int? DisplayOrder { get; set; }
    }

    public class ServiceOrderConfigDto
    {
        public string? Priority { get; set; }
        public string? Notes { get; set; }
        public string? StartDate { get; set; }
        public string? TargetCompletionDate { get; set; }
        public List<int>? InstallationIds { get; set; }
        public Dictionary<string, string>? ItemInstallations { get; set; }
    }

    public class UpdateSaleDto
    {
        public string? Title { get; set; }
        public int? ProjectId { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Stage { get; set; }
        public string? Priority { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Taxes { get; set; }
        public string? TaxType { get; set; }
        public decimal? Discount { get; set; }
        public string? DiscountType { get; set; }
        public decimal? FiscalStamp { get; set; }
        public DateTime? EstimatedCloseDate { get; set; }
        public DateTime? ActualCloseDate { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? BillingAddress { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryPostalCode { get; set; }
        public string? DeliveryCountry { get; set; }
        public string[]? Tags { get; set; }

        public string? LostReason { get; set; }
        public string? MaterialsFulfillment { get; set; }
        public string? ServiceOrdersStatus { get; set; }
        
        // Service order configuration for workflow auto-creation
        public ServiceOrderConfigDto? ServiceOrderConfig { get; set; }
    }

    public class SaleActivityDto
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
    }

    public class CreateSaleActivityDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Type { get; set; } = null!;
        public string? Description { get; set; }
        public string? Details { get; set; }
    }

    public class SaleStatsDto
    {
        public long TotalSales { get; set; }
        public long WonSales { get; set; }
        public long LostSales { get; set; }
        public long ActiveSales { get; set; }
        public decimal TotalValue { get; set; }
        public decimal AverageValue { get; set; }
        public decimal WinRate { get; set; }
        public decimal MonthlyGrowth { get; set; }

        public decimal ConversionRate { get; set; }
    }

    public class PaginatedSaleResponse
    {
        public List<SaleDto> Sales { get; set; } = new();
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
