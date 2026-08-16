using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Purchases.Models
{
    [ModuleScope("purchases")]
    [Table("GoodsReceiptItems")]
    public class GoodsReceiptItem : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("GoodsReceiptId")]
        public int GoodsReceiptId { get; set; }

        [Required]
        [Column("PurchaseOrderItemId")]
        public int PurchaseOrderItemId { get; set; }

        [Column("ArticleId")]
        public int? ArticleId { get; set; }

        [Column("ArticleName")]
        [MaxLength(255)]
        public string? ArticleName { get; set; }

        [Column("ArticleNumber")]
        [MaxLength(50)]
        public string? ArticleNumber { get; set; }

        [Required]
        [Column("OrderedQty", TypeName = "decimal(18,2)")]
        public decimal OrderedQty { get; set; }

        [Required]
        [Column("QuantityReceived", TypeName = "decimal(18,2)")]
        public decimal QuantityReceived { get; set; }

        [Column("QuantityRejected", TypeName = "decimal(18,2)")]
        public decimal QuantityRejected { get; set; } = 0;

        [Column("RejectionReason")]
        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        [Column("LocationId")]
        public int? LocationId { get; set; }

        [Column("Notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation
        [ForeignKey("GoodsReceiptId")]
        public virtual GoodsReceipt? GoodsReceipt { get; set; }

        [ForeignKey("PurchaseOrderItemId")]
        public virtual PurchaseOrderItem? PurchaseOrderItem { get; set; }
    }
}
