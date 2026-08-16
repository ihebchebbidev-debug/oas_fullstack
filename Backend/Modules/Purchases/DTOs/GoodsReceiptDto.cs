using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Purchases.DTOs

{
    public class GoodsReceiptDto
    {
        public int Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public int PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public string Status { get; set; } = "partial";
        public string? DeliveryNoteRef { get; set; }
        public string? Notes { get; set; }
        public string ReceivedBy { get; set; } = string.Empty;
        public string? ReceivedByName { get; set; }
        public List<GoodsReceiptItemDto>? Items { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class GoodsReceiptItemDto
    {
        public int Id { get; set; }
        public int GoodsReceiptId { get; set; }
        public int PurchaseOrderItemId { get; set; }
        public int? ArticleId { get; set; }
        public string? ArticleName { get; set; }
        public string? ArticleNumber { get; set; }
        public decimal OrderedQty { get; set; }
        public decimal QuantityReceived { get; set; }
        public decimal QuantityRejected { get; set; }
        public string? RejectionReason { get; set; }
        public int? LocationId { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateGoodsReceiptDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "PurchaseOrderId must be a positive integer")]
        public int PurchaseOrderId { get; set; }

        public DateTime? ReceiptDate { get; set; }

        [MaxLength(100)] public string? DeliveryNoteRef { get; set; }
        [MaxLength(4000)] public string? Notes { get; set; }

        public List<CreateGoodsReceiptItemDto>? Items { get; set; }
    }

    public class CreateGoodsReceiptItemDto
    {
        [Range(1, int.MaxValue)]
        public int PurchaseOrderItemId { get; set; }

        // Zero received is legitimate (line was ordered but not delivered on
        // this drop) — only reject negatives.
        [Range(0, 9_999_999.9999, ErrorMessage = "QuantityReceived cannot be negative")]
        public decimal QuantityReceived { get; set; }

        [Range(0, 9_999_999.9999, ErrorMessage = "QuantityRejected cannot be negative")]
        public decimal QuantityRejected { get; set; }

        [MaxLength(500)] public string? RejectionReason { get; set; }
        [Range(1, int.MaxValue)] public int? LocationId { get; set; }
        [MaxLength(1000)] public string? Notes { get; set; }
    }

    // Update payload. Items with Id → UPDATE (qty delta re-reconciles PO.ReceivedQty
    // and stock). Items with no Id → APPEND. Existing items absent from the list →
    // REMOVED (their previously received qty is reversed against PO + stock).
    public class UpdateGoodsReceiptDto
    {
        public DateTime? ReceiptDate { get; set; }
        [MaxLength(100)] public string? DeliveryNoteRef { get; set; }
        [MaxLength(4000)] public string? Notes { get; set; }
        public List<UpdateGoodsReceiptItemDto>? Items { get; set; }
    }

    public class UpdateGoodsReceiptItemDto
    {
        public int? Id { get; set; }                       // null/0 → new item
        [Range(1, int.MaxValue)] public int PurchaseOrderItemId { get; set; }
        [Range(0, 9_999_999.9999)] public decimal QuantityReceived { get; set; }
        [Range(0, 9_999_999.9999)] public decimal QuantityRejected { get; set; }
        [MaxLength(500)] public string? RejectionReason { get; set; }
        [Range(1, int.MaxValue)] public int? LocationId { get; set; }
        [MaxLength(1000)] public string? Notes { get; set; }
    }

    public class PaginatedGoodsReceiptResponse

    {
        public List<GoodsReceiptDto> Receipts { get; set; } = new();
        public PurchasePaginationInfo Pagination { get; set; } = new();
    }
}
