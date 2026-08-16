using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Articles.Models
{
    [ModuleScope("articles")]
    [Table("stock_transactions")]
    public class StockTransaction : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("article_id")]
        public int ArticleId { get; set; }

        [Required]
        [Column("transaction_type")]
        [MaxLength(50)]
        public string TransactionType { get; set; } = string.Empty;

        [Required]
        [Column("quantity", TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        [Required]
        [Column("previous_stock", TypeName = "decimal(18,2)")]
        public decimal PreviousStock { get; set; }

        [Required]
        [Column("new_stock", TypeName = "decimal(18,2)")]
        public decimal NewStock { get; set; }

        [Column("reason")]
        [MaxLength(255)]
        public string? Reason { get; set; }

        [Column("reference_type")]
        [MaxLength(50)]
        public string? ReferenceType { get; set; }

        [Column("reference_id")]
        [MaxLength(50)]
        public string? ReferenceId { get; set; }

        [Column("reference_number")]
        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Required]
        [Column("performed_by")]
        [MaxLength(100)]
        public string PerformedBy { get; set; } = string.Empty;

        [Column("performed_by_name")]
        [MaxLength(200)]
        public string? PerformedByName { get; set; }

        [Column("ip_address")]
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        [ForeignKey("ArticleId")]
        public virtual Article? Article { get; set; }
    }
}
