using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Purchases.Models
{
    [ModuleScope("purchases")]
    [Table("SupplierInvoices")]
    public class SupplierInvoice : ITenantEntity, MyApi.Modules.Shared.Models.ISoftDeletable
    {
        public int TenantId { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("InvoiceNumber")]
        [MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Column("SupplierInvoiceRef")]
        [MaxLength(100)]
        public string? SupplierInvoiceRef { get; set; }

        [Required]
        [Column("SupplierId")]
        public int SupplierId { get; set; }

        [Column("SupplierName")]
        [MaxLength(255)]
        public string SupplierName { get; set; } = string.Empty;

        [Column("SupplierMatriculeFiscale")]
        [MaxLength(100)]
        public string? SupplierMatriculeFiscale { get; set; }

        [Column("PurchaseOrderId")]
        public int? PurchaseOrderId { get; set; }

        [Column("GoodsReceiptId")]
        public int? GoodsReceiptId { get; set; }

        [Required]
        [Column("InvoiceDate")]
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("DueDate")]
        public DateTime DueDate { get; set; }

        [Required]
        [Column("Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "draft";
        // draft, validated, partially_paid, paid, cancelled

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

        [Column("AmountPaid", TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; } = 0;

        [Column("PaymentMethod")]
        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        [Column("PaymentDate")]
        public DateTime? PaymentDate { get; set; }

        [Column("Notes")]
        public string? Notes { get; set; }

        // ── Retenue à la Source ──
        [Column("RsApplicable")]
        public bool RsApplicable { get; set; } = false;

        [Column("RsTypeCode")]
        [MaxLength(10)]
        public string? RsTypeCode { get; set; }

        [Column("RsAmount", TypeName = "decimal(18,2)")]
        public decimal RsAmount { get; set; } = 0;

        [Column("RsRecordId")]
        public int? RsRecordId { get; set; }

        // ── TEJ / RiTEJ (DGI cahier de charges v1.0) ──
        /// <summary>Official IdTypeOperation, e.g. "RS1_000001".</summary>
        [Column("RsOperationCode")] [MaxLength(20)] public string? RsOperationCode { get; set; }
        [Column("Cnpc")] [MaxLength(20)] public string? Cnpc { get; set; }
        [Column("PriseEnCharge")] public bool PriseEnCharge { get; set; } = false;
        [Column("AnneeFacturation")] public int? AnneeFacturation { get; set; }
        [Column("RefCertifChezDeclarant")] [MaxLength(50)] public string? RefCertifChezDeclarant { get; set; }
        [Column("RsTvaCode")] [MaxLength(20)] public string? RsTvaCode { get; set; }
        [Column("RsTvaTaux", TypeName = "decimal(5,2)")] public decimal? RsTvaTaux { get; set; }
        [Column("RsTvaAmount", TypeName = "decimal(18,2)")] public decimal RsTvaAmount { get; set; } = 0;
        /// <summary>0=Ajouter (default), 1=Modifier, 2=Annuler.</summary>
        [Column("TejActe")] public short TejActe { get; set; } = 0;

        // ── Facture en Ligne ──
        [Column("FactureEnLigneId")]
        [MaxLength(100)]
        public string? FactureEnLigneId { get; set; }

        [Column("FactureEnLigneStatus")]
        [MaxLength(20)]
        public string? FactureEnLigneStatus { get; set; }
        // pending, sent, validated, rejected

        [Column("FactureEnLigneSentAt")]
        public DateTime? FactureEnLigneSentAt { get; set; }

        // ── TEJ Integration ──
        [Column("TejSynced")]
        public bool TejSynced { get; set; } = false;

        [Column("TejSyncDate")]
        public DateTime? TejSyncDate { get; set; }

        [Column("TejSyncStatus")]
        [MaxLength(20)]
        public string? TejSyncStatus { get; set; } = "pending";
        // pending, synced, error

        [Column("TejErrorMessage")]
        [MaxLength(2000)]
        public string? TejErrorMessage { get; set; }

        [Required]
        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [Column("ModifiedDate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("ModifiedBy")]
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        // Client-supplied Idempotency-Key header value. Per-tenant unique index
        // (see the IdempotencyKey columns in Modules/Purchases/Database/migration.sql) makes a retried POST with
        // the same key return the existing invoice instead of a duplicate.
        [Column("IdempotencyKey")]
        [MaxLength(64)]
        public string? IdempotencyKey { get; set; }

        // Navigation
        [ForeignKey("SupplierId")]
        public virtual MyApi.Modules.Contacts.Models.Contact? Supplier { get; set; }

        [ForeignKey("PurchaseOrderId")]
        public virtual PurchaseOrder? PurchaseOrder { get; set; }

        [ForeignKey("GoodsReceiptId")]
        public virtual GoodsReceipt? GoodsReceipt { get; set; }

        public virtual ICollection<SupplierInvoiceItem>? Items { get; set; }
    }
}
