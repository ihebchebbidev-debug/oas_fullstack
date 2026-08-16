using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Invoices.Models
{
    /// <summary>
    /// Single-entry invoice line. SourceType/SourceId keep provenance back to the
    /// upstream ledger row (sale_item, service_order_material, service_order_time_entry,
    /// service_order_expense, dispatch_material, dispatch_expense, dispatch_time_entry).
    /// </summary>
    [ModuleScope("invoices")]
    [Table("InvoiceLines")]
    public class InvoiceLine : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("InvoiceId")]
        public int InvoiceId { get; set; }

        [Column("SourceType")]
        [MaxLength(50)]
        public string? SourceType { get; set; }

        [Column("SourceId")]
        [MaxLength(100)]
        public string? SourceId { get; set; }

        [Required]
        [Column("ItemName")]
        [MaxLength(255)]
        public string ItemName { get; set; } = string.Empty;

        [Column("Description")]
        public string? Description { get; set; }

        [Column("Quantity", TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; } = 1;

        [Column("Unit")]
        [MaxLength(20)]
        public string? Unit { get; set; }

        [Column("UnitPrice", TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column("TaxRate", TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; }

        [Column("LineTotal", TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [Column("TaxAmount", TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column("DisplayOrder")]
        public int DisplayOrder { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("InvoiceId")]
        public virtual Invoice? Invoice { get; set; }
    }
}