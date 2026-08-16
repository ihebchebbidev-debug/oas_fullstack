using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Purchases.Models
{
    [ModuleScope("purchases")]
    [Table("PurchaseOrderItems")]
    public class PurchaseOrderItem : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("PurchaseOrderId")]
        public int PurchaseOrderId { get; set; }

        [Column("ArticleId")]
        public int? ArticleId { get; set; }

        [Column("ArticleName")]
        [MaxLength(255)]
        public string? ArticleName { get; set; }

        [Column("ArticleNumber")]
        [MaxLength(50)]
        public string? ArticleNumber { get; set; }

        [Column("SupplierRef")]
        [MaxLength(100)]
        public string? SupplierRef { get; set; }

        [Required]
        [Column("Description")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column("Quantity", TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; } = 1;

        [Column("ReceivedQty", TypeName = "decimal(18,2)")]
        public decimal ReceivedQty { get; set; } = 0;

        [Required]
        [Column("UnitPrice", TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } = 0;

        [Column("TaxRate", TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } = 19;

        [Column("Discount", TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; } = 0;

        [Column("DiscountType")]
        [MaxLength(20)]
        public string DiscountType { get; set; } = "percentage";

        [Column("LineTotal", TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; } = 0;

        [Column("Unit")]
        [MaxLength(20)]
        public string Unit { get; set; } = "piece";

        [Column("DisplayOrder")]
        public int DisplayOrder { get; set; } = 0;

        // Navigation
        [ForeignKey("PurchaseOrderId")]
        public virtual PurchaseOrder? PurchaseOrder { get; set; }
    }
}
