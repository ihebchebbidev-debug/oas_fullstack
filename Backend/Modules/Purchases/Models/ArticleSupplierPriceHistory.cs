using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Purchases.Models
{
    [ModuleScope("purchases")]
    [Table("ArticleSupplierPriceHistory")]
    public class ArticleSupplierPriceHistory : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("ArticleSupplierId")]
        public int ArticleSupplierId { get; set; }

        [Required]
        [Column("OldPrice", TypeName = "decimal(18,2)")]
        public decimal OldPrice { get; set; }

        [Required]
        [Column("NewPrice", TypeName = "decimal(18,2)")]
        public decimal NewPrice { get; set; }

        [Column("Currency")]
        [MaxLength(3)]
        public string Currency { get; set; } = "TND";

        [Required]
        [Column("ChangedAt")]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("ChangedBy")]
        [MaxLength(100)]
        public string ChangedBy { get; set; } = string.Empty;

        [Column("Reason")]
        [MaxLength(500)]
        public string? Reason { get; set; }

        // Navigation
        [ForeignKey("ArticleSupplierId")]
        public virtual ArticleSupplier? ArticleSupplier { get; set; }
    }
}
