using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Articles.Models
{
    [ModuleScope("articles")]
    [Table("Articles")]
    public class Article : ITenantEntity, MyApi.Modules.Shared.Models.ISoftDeletable
    {
        public int TenantId { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ArticleNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? Description { get; set; }

        public int? CategoryId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Unit { get; set; } = "piece";

        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SalesPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal StockQuantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinStockLevel { get; set; }

        public int? LocationId { get; set; }

        // GroupId now references LookupItems where LookupType='article-groups'
        // This allows articles to use the generic Lookups system instead of a dedicated ArticleGroups table
        public int? GroupId { get; set; }

        [MaxLength(200)]
        public string? Supplier { get; set; }

        [MaxLength(20)]
        public string Type { get; set; } = "material";

        public int? Duration { get; set; }  // Duration in minutes for service-type articles

        [Column("skills_required")]
        public string? SkillsRequired { get; set; }  // JSON-encoded string[] of required skill names

        [Column(TypeName = "decimal(5,2)")]
        public decimal TvaRate { get; set; } = 19.00m;  // TVA/VAT rate in percentage (default 19%)

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }
    }
}
