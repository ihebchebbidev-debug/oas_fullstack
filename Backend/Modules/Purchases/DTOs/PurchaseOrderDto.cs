using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Purchases.DTOs

{
    // ─── Purchase Order ───

    public class PurchaseOrderDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierEmail { get; set; }
        public string? SupplierPhone { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierMatriculeFiscale { get; set; }
        public string Status { get; set; } = "draft";
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDelivery { get; set; }
        public DateTime? ActualDelivery { get; set; }
        public string Currency { get; set; } = "TND";
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public string DiscountType { get; set; } = "percentage";
        public decimal TaxAmount { get; set; }
        public decimal FiscalStamp { get; set; }
        public decimal GrandTotal { get; set; }
        public string PaymentTerms { get; set; } = "net30";
        public string PaymentStatus { get; set; } = "pending";
        public string? Notes { get; set; }
        public string[]? Tags { get; set; }
        public string? BillingAddress { get; set; }
        public string? DeliveryAddress { get; set; }
        public int? ServiceOrderId { get; set; }
        public int? SaleId { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime? SentToSupplierAt { get; set; }
        public List<PurchaseOrderItemDto>? Items { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class PurchaseOrderItemDto
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public int? ArticleId { get; set; }
        public string? ArticleName { get; set; }
        public string? ArticleNumber { get; set; }
        public string? SupplierRef { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ReceivedQty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; }
        public decimal Discount { get; set; }
        public string DiscountType { get; set; } = "percentage";
        public decimal LineTotal { get; set; }
        public string Unit { get; set; } = "piece";
        public int DisplayOrder { get; set; }
    }

    public class CreatePurchaseOrderDto
    {
        [MaxLength(255)] public string? Title { get; set; }
        [MaxLength(4000)] public string? Description { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "SupplierId must be a positive integer")]
        public int SupplierId { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "TND";

        // Frontend sends the user-picked order date (e.g. backdating a PO recorded
        // a few days late). Previously this field was missing from the DTO and
        // the server silently stamped DateTime.UtcNow, throwing away the user's
        // input — confusing for accountants who need the booked date to match
        // the supplier's paperwork.
        public DateTime? OrderDate { get; set; }
        public DateTime? ExpectedDelivery { get; set; }

        [Range(0, 9_999_999_999.99, ErrorMessage = "Discount cannot be negative")]
        public decimal Discount { get; set; }

        [RegularExpression("^(percentage|fixed)$")]
        public string DiscountType { get; set; } = "percentage";

        [Range(0, 9_999_999.999, ErrorMessage = "FiscalStamp cannot be negative")]
        public decimal FiscalStamp { get; set; } = 1.000m;

        [MaxLength(50)]
        public string PaymentTerms { get; set; } = "net30";

        [MaxLength(4000)] public string? Notes { get; set; }
        public string[]? Tags { get; set; }
        [MaxLength(1000)] public string? BillingAddress { get; set; }
        [MaxLength(1000)] public string? DeliveryAddress { get; set; }
        [Range(1, int.MaxValue)] public int? ServiceOrderId { get; set; }
        [Range(1, int.MaxValue)] public int? SaleId { get; set; }
        public List<CreatePurchaseOrderItemDto>? Items { get; set; }
    }

    public class CreatePurchaseOrderItemDto
    {
        [Range(1, int.MaxValue)] public int? ArticleId { get; set; }
        [MaxLength(255)] public string? ArticleName { get; set; }
        [MaxLength(100)] public string? ArticleNumber { get; set; }
        [MaxLength(100)] public string? SupplierRef { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Description is required")]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Range(0.0001, 9_999_999.9999, ErrorMessage = "Quantity must be greater than zero")]
        public decimal Quantity { get; set; } = 1;

        [Range(0, 9_999_999_999.9999, ErrorMessage = "UnitPrice cannot be negative")]
        public decimal UnitPrice { get; set; }

        [Range(0, 100, ErrorMessage = "TaxRate must be between 0 and 100")]
        public decimal TaxRate { get; set; } = 19;

        [Range(0, 9_999_999_999.99)]
        public decimal Discount { get; set; }

        [RegularExpression("^(percentage|fixed)$")]
        public string DiscountType { get; set; } = "percentage";

        [MaxLength(20)]
        public string Unit { get; set; } = "piece";
    }

    public class UpdatePurchaseOrderDto
    {
        [MaxLength(255)] public string? Title { get; set; }
        [MaxLength(4000)] public string? Description { get; set; }
        [MaxLength(30)] public string? Status { get; set; }
        public DateTime? ExpectedDelivery { get; set; }
        [Range(0, 9_999_999_999.99)] public decimal? Discount { get; set; }
        [RegularExpression("^(percentage|fixed)$")] public string? DiscountType { get; set; }
        [Range(0, 9_999_999.999)] public decimal? FiscalStamp { get; set; }
        [MaxLength(50)] public string? PaymentTerms { get; set; }
        // NOTE: PaymentStatus is intentionally NOT updatable here — it is derived
        // from the linked SupplierInvoices by
        // SupplierInvoiceService.SyncPurchaseOrderPaymentStatusAsync.
        [MaxLength(4000)] public string? Notes { get; set; }
        public string[]? Tags { get; set; }
        [MaxLength(1000)] public string? BillingAddress { get; set; }
        [MaxLength(1000)] public string? DeliveryAddress { get; set; }
    }


    public class PurchaseOrderStatsDto
    {
        public long TotalOrders { get; set; }
        public long DraftOrders { get; set; }
        public long OrderedOrders { get; set; }
        public long ReceivedOrders { get; set; }
        public long CancelledOrders { get; set; }
        public decimal TotalSpend { get; set; }
        public decimal MonthlySpend { get; set; }
        /// <summary>Spend since Jan 1st of the current year (excludes draft + cancelled).</summary>
        public decimal TotalSpendThisYear { get; set; }
        public decimal AvgLeadTime { get; set; }
        public long PendingReceipts { get; set; }
        public long OverdueInvoices { get; set; }
        /// <summary>Supplier invoices that are neither paid nor cancelled.</summary>
        public long OpenInvoices { get; set; }
        /// <summary>Total withholding tax (retenue à la source) on supplier invoices.</summary>
        public decimal RsTotal { get; set; }
    }

    public class PaginatedPurchaseOrderResponse
    {
        public List<PurchaseOrderDto> Orders { get; set; } = new();
        public PurchasePaginationInfo Pagination { get; set; } = new();
    }

    public class PurchasePaginationInfo
    {
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
    }
}
