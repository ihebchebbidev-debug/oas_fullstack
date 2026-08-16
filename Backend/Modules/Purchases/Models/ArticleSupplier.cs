using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Purchases.Models
{
    [ModuleScope("purchases")]
    [Table("ArticleSuppliers")]
    public class ArticleSupplier : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("ArticleId")]
        public int ArticleId { get; set; }

        [Required]
        [Column("SupplierId")]
        public int SupplierId { get; set; }

        [Column("SupplierRef")]
        [MaxLength(100)]
        public string? SupplierRef { get; set; }

        [Required]
        [Column("PurchasePrice", TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column("Currency")]
        [MaxLength(3)]
        public string Currency { get; set; } = "TND";

        [Column("MinOrderQty", TypeName = "decimal(18,2)")]
        public decimal MinOrderQty { get; set; } = 1;

        [Column("LeadTimeDays")]
        public int LeadTimeDays { get; set; } = 0;

        [Column("IsPreferred")]
        public bool IsPreferred { get; set; } = false;

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("Notes")]
        public string? Notes { get; set; }

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

        // Soft-delete columns. Hard delete used to drop the row (and, via the
        // previous Cascade FK, its entire price history). Now we tombstone the
        // row and preserve ArticleSupplierPriceHistory for audit / reporting.
        [Column("IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("DeletedAt")]
        public DateTime? DeletedAt { get; set; }

        [Column("DeletedBy")]
        [MaxLength(100)]
        public string? DeletedBy { get; set; }

        // Navigation
        [ForeignKey("ArticleId")]
        public virtual MyApi.Modules.Articles.Models.Article? Article { get; set; }

        [ForeignKey("SupplierId")]
        public virtual MyApi.Modules.Contacts.Models.Contact? Supplier { get; set; }

        public virtual ICollection<ArticleSupplierPriceHistory> PriceHistory { get; set; } = new List<ArticleSupplierPriceHistory>();
    }
}
