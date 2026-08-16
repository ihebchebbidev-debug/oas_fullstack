using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.Shared.Models;

namespace MyApi.Modules.Invoices.Models
{
    /// <summary>
    /// Customer invoice header. Single-entry ledger: header + lines. States:
    ///   draft   – created, editable, not numbered yet
    ///   posted  – finalised, numbered, immutable (only payments/void allowed)
    ///   paid    – all amount paid (posted total == AmountPaid)
    ///   void    – cancelled after posting; keeps number for audit
    /// Lineage is tracked by SaleId / ServiceOrderId. ContactId is denormalised
    /// for filtering and reporting.
    /// </summary>
    [ModuleScope("invoices")]
    [Table("Invoices")]
    public class Invoice : ITenantEntity, ISoftDeletable
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
        public string? InvoiceNumber { get; set; }

        [Required]
        [Column("Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "draft";

        [Required]
        [Column("ContactId")]
        public int ContactId { get; set; }

        [Column("SaleId")]
        public int? SaleId { get; set; }

        [Column("ServiceOrderId")]
        public int? ServiceOrderId { get; set; }

        [Column("Title")]
        [MaxLength(255)]
        public string? Title { get; set; }

        [Column("Notes")]
        public string? Notes { get; set; }

        [Column("Currency")]
        [MaxLength(10)]
        public string Currency { get; set; } = "TND";

        [Column("Subtotal", TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column("TaxAmount", TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column("GrandTotal", TypeName = "decimal(18,2)")]
        public decimal GrandTotal { get; set; }

        [Column("AmountPaid", TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [Column("IssueDate")]
        public DateTime? IssueDate { get; set; }

        [Column("DueDate")]
        public DateTime? DueDate { get; set; }

        [Column("PostedAt")]
        public DateTime? PostedAt { get; set; }

        [Column("VoidedAt")]
        public DateTime? VoidedAt { get; set; }

        [Column("VoidReason")]
        [MaxLength(500)]
        public string? VoidReason { get; set; }

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    }
}