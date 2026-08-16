using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Purchases.Models
{
    [ModuleScope("purchases")]
    [Table("SupplierInvoiceItems")]
    public class SupplierInvoiceItem : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("SupplierInvoiceId")]
        public int SupplierInvoiceId { get; set; }

        [Column("PurchaseOrderItemId")]
        public int? PurchaseOrderItemId { get; set; }

        [Column("ArticleId")]
        public int? ArticleId { get; set; }

        [Column("ArticleName")]
        [MaxLength(255)]
        public string? ArticleName { get; set; }

        [Required]
        [Column("Description")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column("Quantity", TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; } = 1;

        [Required]
        [Column("UnitPrice", TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } = 0;

        [Column("TaxRate", TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } = 19;

        [Column("LineTotal", TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; } = 0;

        [Column("DisplayOrder")]
        public int DisplayOrder { get; set; } = 0;

        // Navigation
        [ForeignKey("SupplierInvoiceId")]
        public virtual SupplierInvoice? SupplierInvoice { get; set; }
    }
}
