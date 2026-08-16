using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Planning.Models
{
    /// <summary>
    /// Planned time or expense entry attached to an offer item, sale item, or service order job.
    /// Carries lineage from the originating offer line through every conversion so plan-vs-actual
    /// can be computed at execution time on the dispatch.
    /// </summary>
    [ModuleScope("planning")]
    [Table("PlannedLineEntries")]
    public class PlannedLineEntry : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>offer_item | sale_item | service_order_job</summary>
        [Required, MaxLength(30)]
        public string ParentType { get; set; } = string.Empty;

        [Required]
        public int ParentId { get; set; }

        /// <summary>Stable lineage anchor — the offer_item id where this plan was first authored.</summary>
        public int? OriginOfferItemId { get; set; }

        /// <summary>time | expense</summary>
        [Required, MaxLength(20)]
        public string Kind { get; set; } = string.Empty;

        // ---- time fields ----
        public int? PlannedMinutes { get; set; }
        public int? TechnicianCount { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? HourlyRate { get; set; }

        // ---- expense fields ----
        /// <summary>travel | per_diem | materials | subcontractor</summary>
        [MaxLength(30)]
        public string? ExpenseType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PlannedAmount { get; set; }

        [MaxLength(3)]
        public string? Currency { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        // ---- material fields (kind = "material") ----
        public int? ArticleId { get; set; }

        [MaxLength(200)]
        public string? ArticleName { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? UnitPrice { get; set; }

        [MaxLength(20)]
        public string? Unit { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }
    }
}
