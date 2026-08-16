using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Purchases.DTOs
{
    public class SupplierInvoiceDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? SupplierInvoiceRef { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierMatriculeFiscale { get; set; }
        public int? PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public int? GoodsReceiptId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "draft";
        public string Currency { get; set; } = "TND";
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public string DiscountType { get; set; } = "percentage";
        public decimal TaxAmount { get; set; }
        public decimal FiscalStamp { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? Notes { get; set; }
        // Retenue à la source
        public bool RsApplicable { get; set; }
        public string? RsTypeCode { get; set; }
        public decimal RsAmount { get; set; }
        public int? RsRecordId { get; set; }
        // TEJ / RiTEJ (DGI cahier de charges)
        public string? RsOperationCode { get; set; }
        public string? Cnpc { get; set; }
        public bool PriseEnCharge { get; set; }
        public int? AnneeFacturation { get; set; }
        public string? RefCertifChezDeclarant { get; set; }
        public string? RsTvaCode { get; set; }
        public decimal? RsTvaTaux { get; set; }
        public decimal RsTvaAmount { get; set; }
        public short TejActe { get; set; }
        // Facture en ligne
        public string? FactureEnLigneId { get; set; }
        public string? FactureEnLigneStatus { get; set; }
        public DateTime? FactureEnLigneSentAt { get; set; }
        // TEJ
        public bool TejSynced { get; set; }
        public DateTime? TejSyncDate { get; set; }
        public string? TejSyncStatus { get; set; }
        public string? TejErrorMessage { get; set; }
        public List<SupplierInvoiceItemDto>? Items { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class SupplierInvoiceItemDto
    {
        public int Id { get; set; }
        public int SupplierInvoiceId { get; set; }
        public int? PurchaseOrderItemId { get; set; }
        public int? ArticleId { get; set; }
        public string? ArticleName { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; }
        public decimal LineTotal { get; set; }
        public int DisplayOrder { get; set; }
    }

    // NOTE on validation:
    // Data-annotation attributes below are enforced by [ApiController] model
    // binding — the request short-circuits with a 400 ProblemDetails before
    // the service is ever invoked. The service ALSO re-checks the same bounds
    // (defense in depth) so any programmatic caller that bypasses model binding
    // still can't slip a negative quantity into the totals recalculation.
    public class CreateSupplierInvoiceDto
    {
        [MaxLength(100)]
        public string? SupplierInvoiceRef { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "SupplierId must be a positive integer")]
        public int SupplierId { get; set; }

        [Range(1, int.MaxValue)]
        public int? PurchaseOrderId { get; set; }

        [Range(1, int.MaxValue)]
        public int? GoodsReceiptId { get; set; }

        [Required]
        public DateTime InvoiceDate { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "TND";

        [Range(0, 9_999_999_999.99, ErrorMessage = "Discount cannot be negative")]
        public decimal Discount { get; set; }

        [RegularExpression("^(percentage|fixed)$", ErrorMessage = "DiscountType must be 'percentage' or 'fixed'")]
        public string DiscountType { get; set; } = "percentage";

        [Range(0, 9_999_999.999, ErrorMessage = "FiscalStamp cannot be negative")]
        public decimal FiscalStamp { get; set; } = 1.000m;

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        [MaxLength(4000)]
        public string? Notes { get; set; }

        public bool RsApplicable { get; set; }
        [MaxLength(10)] public string? RsTypeCode { get; set; }
        [MaxLength(20)] public string? RsOperationCode { get; set; }
        [MaxLength(20)] public string? Cnpc { get; set; }
        public bool PriseEnCharge { get; set; }
        [Range(2000, 2100)] public int? AnneeFacturation { get; set; }
        [MaxLength(20)] public string? RsTvaCode { get; set; }
        [Range(0, 100, ErrorMessage = "RsTvaTaux must be between 0 and 100")]
        public decimal? RsTvaTaux { get; set; }

        public List<CreateSupplierInvoiceItemDto>? Items { get; set; }
    }

    public class CreateSupplierInvoiceItemDto
    {
        [Range(1, int.MaxValue)]
        public int? PurchaseOrderItemId { get; set; }

        [Range(1, int.MaxValue)]
        public int? ArticleId { get; set; }

        [MaxLength(255)]
        public string? ArticleName { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Description is required")]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        // Quantity MUST be strictly positive — a zero-qty line contributes
        // nothing to totals and is almost always a UI bug, not a real intent.
        [Range(0.0001, 9_999_999.9999, ErrorMessage = "Quantity must be greater than zero")]
        public decimal Quantity { get; set; } = 1;

        [Range(0, 9_999_999_999.9999, ErrorMessage = "UnitPrice cannot be negative")]
        public decimal UnitPrice { get; set; }

        [Range(0, 100, ErrorMessage = "TaxRate must be between 0 and 100")]
        public decimal TaxRate { get; set; } = 19;
    }

    public class UpdateSupplierInvoiceDto
    {
        [MaxLength(100)] public string? SupplierInvoiceRef { get; set; }
        [MaxLength(30)]  public string? Status { get; set; }
        public DateTime? DueDate { get; set; }
        [Range(0, 9_999_999_999.99)] public decimal? Discount { get; set; }
        [RegularExpression("^(percentage|fixed)$")] public string? DiscountType { get; set; }
        [Range(0, 9_999_999.999)] public decimal? FiscalStamp { get; set; }
        [MaxLength(50)] public string? PaymentMethod { get; set; }
        [Range(0, 9_999_999_999.99, ErrorMessage = "AmountPaid cannot be negative")]
        public decimal? AmountPaid { get; set; }
        public DateTime? PaymentDate { get; set; }
        [MaxLength(4000)] public string? Notes { get; set; }
        public bool? RsApplicable { get; set; }
        [MaxLength(10)] public string? RsTypeCode { get; set; }
        [MaxLength(20)] public string? RsOperationCode { get; set; }
        [MaxLength(20)] public string? Cnpc { get; set; }
        public bool? PriseEnCharge { get; set; }
        [Range(2000, 2100)] public int? AnneeFacturation { get; set; }
        [MaxLength(50)] public string? RefCertifChezDeclarant { get; set; }
        [MaxLength(20)] public string? RsTvaCode { get; set; }
        [Range(0, 100)] public decimal? RsTvaTaux { get; set; }
        public short? TejActe { get; set; }
        // TEJ sync (Tunisian e-tax journal)
        public bool? TejSynced { get; set; }
        public DateTime? TejSyncDate { get; set; }
        [MaxLength(20)] public string? TejSyncStatus { get; set; }
        [MaxLength(2000)] public string? TejErrorMessage { get; set; }
        // Facture en ligne
        [MaxLength(100)] public string? FactureEnLigneId { get; set; }
        [MaxLength(20)] public string? FactureEnLigneStatus { get; set; }
        public DateTime? FactureEnLigneSentAt { get; set; }
    }

    public class PaginatedSupplierInvoiceResponse
    {
        public List<SupplierInvoiceDto> Invoices { get; set; } = new();
        public PurchasePaginationInfo Pagination { get; set; } = new();
    }

    /// <summary>
    /// Payload for recording a Facture en Ligne (TTN) submission that the user performed
    /// on the TTN portal. The reference is mandatory — the system does not transmit to TTN.
    /// </summary>
    public class RecordFactureEnLigneDto
    {
        [Required] [MaxLength(100)] public string? FactureEnLigneId { get; set; }
        [MaxLength(20)] public string? Status { get; set; }
        public DateTime? SentAt { get; set; }
    }
}
