using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Articles.Models
{
    [ModuleScope("articles")]
    [Table("InventoryTransactions")]
    public class InventoryTransaction : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ArticleId { get; set; }

        [Required]
        [MaxLength(20)]
        public string TransactionType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? Reference { get; set; }

        [Column(TypeName = "text")]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        // Navigation property
        [ForeignKey("ArticleId")]
        public virtual Article? Article { get; set; }
    }
}