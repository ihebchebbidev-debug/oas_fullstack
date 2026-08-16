using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Purchases.Models
{
    [ModuleScope("purchases")]
    [Table("PurchaseOrders")]
    public class PurchaseOrder : ITenantEntity, MyApi.Modules.Shared.Models.ISoftDeletable
    {
        public int TenantId { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("OrderNumber")]
        [MaxLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        [Column("Title")]
        [MaxLength(255)]
        public string? Title { get; set; }

        [Column("Description")]
        public string? Description { get; set; }

        [Required]
        [Column("SupplierId")]
        public int SupplierId { get; set; }

        [Column("SupplierName")]
        [MaxLength(255)]
        public string SupplierName { get; set; } = string.Empty;

        [Column("SupplierEmail")]
        [MaxLength(255)]
        public string? SupplierEmail { get; set; }

        [Column("SupplierPhone")]
        [MaxLength(20)]
        public string? SupplierPhone { get; set; }

        [Column("SupplierAddress")]
        [MaxLength(500)]
        public string? SupplierAddress { get; set; }

        [Column("SupplierMatriculeFiscale")]
        [MaxLength(100)]
        public string? SupplierMatriculeFiscale { get; set; }

        [Required]
        [Column("Status")]
        [MaxLength(30)]
        public string Status { get; set; } = "draft";
        // draft, validated, ordered, partially_received, received, cancelled

        [Column("OrderDate")]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Column("ExpectedDelivery")]
        public DateTime? ExpectedDelivery { get; set; }

        [Column("ActualDelivery")]
        public DateTime? ActualDelivery { get; set; }

        [Column("Currency")]
        [MaxLength(3)]
        public string Currency { get; set; } = "TND";

        [Column("SubTotal", TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; } = 0;

        [Column("Discount", TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; } = 0;

        [Column("DiscountType")]
        [MaxLength(20)]
        public string DiscountType { get; set; } = "percentage";

        [Column("TaxAmount", TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0;

        [Column("FiscalStamp", TypeName = "decimal(10,3)")]
        public decimal FiscalStamp { get; set; } = 1.000m;

        [Column("GrandTotal", TypeName = "decimal(18,2)")]
        public decimal GrandTotal { get; set; } = 0;

        [Column("PaymentTerms")]
        [MaxLength(50)]
        public string PaymentTerms { get; set; } = "net30";

        [Column("PaymentStatus")]
        [MaxLength(20)]
        public string PaymentStatus { get; set; } = "pending";
        // pending, partial, paid

        [Column("Notes")]
        public string? Notes { get; set; }

        [Column("Tags")]
        public string[]? Tags { get; set; }

        [Column("BillingAddress")]
        public string? BillingAddress { get; set; }

        [Column("DeliveryAddress")]
        public string? DeliveryAddress { get; set; }

        [Column("ServiceOrderId")]
        public int? ServiceOrderId { get; set; }

        [Column("SaleId")]
        public int? SaleId { get; set; }

        [Column("ApprovedBy")]
        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        [Column("ApprovalDate")]
        public DateTime? ApprovalDate { get; set; }

        [Column("SentToSupplierAt")]
        public DateTime? SentToSupplierAt { get; set; }

        [Required]
        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [Column("CreatedByName")]
        [MaxLength(255)]
        public string? CreatedByName { get; set; }

        [Column("ModifiedDate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("ModifiedBy")]
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        // Client-supplied Idempotency-Key header value. Per-tenant unique index
        // (see the IdempotencyKey columns in Modules/Purchases/Database/migration.sql) makes a retried POST with
        // the same key return the existing PO instead of minting a duplicate.
        [Column("IdempotencyKey")]
        [MaxLength(64)]
        public string? IdempotencyKey { get; set; }


        // Navigation
        [ForeignKey("SupplierId")]
        public virtual MyApi.Modules.Contacts.Models.Contact? Supplier { get; set; }

        public virtual ICollection<PurchaseOrderItem>? Items { get; set; }
        // Polymorphic — see PurchaseConfiguration. Must stay [NotMapped] so EF
        // does not infer a shadow PurchaseOrderId FK on PurchaseActivities,
        // which causes "column PurchaseOrderId of relation PurchaseActivities
        // does not exist" on every insert.
        [NotMapped]
        public virtual ICollection<PurchaseActivity>? Activities { get; set; }
    }
}
