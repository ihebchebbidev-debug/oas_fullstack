namespace MyApi.Modules.Offers.DTOs
{
    public class OfferDto
    {
        public int Id { get; set; }
        public string? OfferNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        
        // Contact Information
        public int ContactId { get; set; }
        public int? ProjectId { get; set; }
        public ContactSummaryDto? Contact { get; set; }
        
        // Financial Information
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TND";
        public decimal? Taxes { get; set; }
        public string TaxType { get; set; } = "percentage";
        public decimal? Discount { get; set; }
        public string DiscountType { get; set; } = "percentage";
        public decimal? FiscalStamp { get; set; }
        public decimal? TotalAmount { get; set; }
        
        // Status & Classification
        public string Status { get; set; } = "draft";
        public string? Category { get; set; }
        public string? Source { get; set; }
        
        // Addresses
        public string? BillingAddress { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryPostalCode { get; set; }
        public string? DeliveryCountry { get; set; }
        
        // Validity
        public DateTime? ValidUntil { get; set; }
        
        // Assignment
        public string? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        
        // Tags
        public string[]? Tags { get; set; }
        
        // Items
        public List<OfferItemDto>? Items { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime? LastActivity { get; set; }
        
        // Conversion
        public string? ConvertedToSaleId { get; set; }
        public string? ConvertedToServiceOrderId { get; set; }
        public DateTime? ConvertedAt { get; set; }
        
        // Tracking
        public int SentCount { get; set; }
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

    public class OfferItemDto
    {
        public int Id { get; set; }
        public int OfferId { get; set; }
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
        public int DisplayOrder { get; set; }
    }

    public class CreateOfferDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ContactId { get; set; }
        public int? ProjectId { get; set; }
        public string Status { get; set; } = "draft";
        public string? Category { get; set; }
        public string? Source { get; set; }
        public string Currency { get; set; } = "TND";
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
        public string? Notes { get; set; }
        public List<CreateOfferItemDto>? Items { get; set; }
    }

    public class CreateOfferItemDto
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
        public int? DisplayOrder { get; set; }
    }

    public class UpdateOfferDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? ContactId { get; set; }
        public int? ProjectId { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Source { get; set; }
        public string? Currency { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Taxes { get; set; }
        public string? TaxType { get; set; }
        public decimal? Discount { get; set; }
        public string? DiscountType { get; set; }
        public decimal? FiscalStamp { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? BillingAddress { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryPostalCode { get; set; }
        public string? DeliveryCountry { get; set; }
        public string[]? Tags { get; set; }
    }

    public class OfferActivityDto
    {
        public int Id { get; set; }
        public int OfferId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
    }

    public class CreateOfferActivityDto
    {
        public string Type { get; set; } = "note";
        public string Description { get; set; } = string.Empty;
        public string? Details { get; set; }
    }

    public class ConvertOfferDto
    {
        public bool ConvertToSale { get; set; }
        public bool ConvertToServiceOrder { get; set; }
        public object? SalesData { get; set; }
        public object? ServiceOrderData { get; set; }
    }

    public class OfferStatsDto
    {
        public long TotalOffers { get; set; }
        public long ActiveOffers { get; set; }
        public long AcceptedOffers { get; set; }
        public long DeclinedOffers { get; set; }
        public decimal TotalValue { get; set; }
        public decimal AverageValue { get; set; }
        public decimal ConversionRate { get; set; }
        public decimal MonthlyGrowth { get; set; }
    }

    public class PaginatedOfferResponse
    {
        public List<OfferDto> Offers { get; set; } = new();
        public PaginationInfo Pagination { get; set; } = new();
    }

    public class PaginationInfo
    {
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
    }

    // =====================================================
    // Bulk Import DTOs - Supports up to 10,000+ records
    // =====================================================

    public class BulkImportOfferRequestDto
    {
        public List<CreateOfferDto> Offers { get; set; } = new();
        
        /// <summary>
        /// If true, skip offers with duplicate title/contact combination instead of failing
        /// </summary>
        public bool SkipDuplicates { get; set; } = true;
    }

    public class BulkImportOfferResultDto
    {
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<OfferDto> ImportedOffers { get; set; } = new();
    }
}
