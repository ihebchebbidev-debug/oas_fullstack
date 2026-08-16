using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Deals.Models
{
    /// <summary>Optional line item on a deal — links to an article of type "article" or "service".</summary>
    [ModuleScope("deals")]
    [Table("DealItems")]
    public class DealItem : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DealId")]
        public int DealId { get; set; }

        [Column("ArticleId")]
        public int? ArticleId { get; set; }

        [Required]
        [Column("Type")]
        [MaxLength(20)]
        public string Type { get; set; } = "article"; // "article" | "service"

        [Column("ItemName")]
        [MaxLength(255)]
        public string ItemName { get; set; } = string.Empty;

        [Column("ItemCode")]
        [MaxLength(100)]
        public string? ItemCode { get; set; }

        [Column("Description")]
        public string? Description { get; set; }

        [Column("Quantity", TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; } = 1;

        [Column("UnitPrice", TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } = 0;

        [Column("Discount", TypeName = "decimal(5,2)")]
        public decimal Discount { get; set; } = 0;

        [Column("DiscountType")]
        [MaxLength(20)]
        public string DiscountType { get; set; } = "percentage";

        [Column("LineTotal", TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; } = 0;

        [Column("DisplayOrder")]
        public int DisplayOrder { get; set; } = 0;

        // Optional link to a physical installation/equipment — mirrors offer & sale items.
        [Column("InstallationId")]
        [MaxLength(50)]
        public string? InstallationId { get; set; }

        [Column("InstallationName")]
        [MaxLength(255)]
        public string? InstallationName { get; set; }

        // Navigation
        public virtual Deal? Deal { get; set; }
    }
}
