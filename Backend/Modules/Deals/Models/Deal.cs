using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Deals.Models
{
    /// <summary>
    /// A Deal is the single source of truth for a sales opportunity / pipeline entry.
    /// It sits BEFORE an Offer in the funnel and can graduate into an Offer, a Sale or a Project.
    /// Deliberately lighter than an Offer (no fiscal-document fields) so the two never blur.
    /// </summary>
    [ModuleScope("deals")]
    [Table("Deals")]
    public class Deal : ITenantEntity, MyApi.Modules.Shared.Models.ISoftDeletable
    {
        public int TenantId { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("DealNumber")]
        [MaxLength(50)]
        public string? DealNumber { get; set; }

        [Column("Title")]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Column("Description")]
        public string? Description { get; set; }

        [Required]
        [Column("ContactId")]
        public int ContactId { get; set; }

        /// <summary>Project this deal belongs to (a project is a container of deals).</summary>
        [Column("ProjectId")]
        public int? ProjectId { get; set; }

        /// <summary>Pipeline stage: lead / qualified / proposal / negotiation / won / lost.</summary>
        [Required]
        [Column("Stage", TypeName = "text")]
        public string Stage { get; set; } = "lead";

        [Column("Probability")]
        public int Probability { get; set; } = 0;

        [Column("EstimatedValue", TypeName = "decimal(18,2)")]
        public decimal EstimatedValue { get; set; } = 0;

        [Column("Currency")]
        [MaxLength(3)]
        public string Currency { get; set; } = "TND";

        [Column("ExpectedCloseDate")]
        public DateTime? ExpectedCloseDate { get; set; }

        [Column("ActualCloseDate")]
        public DateTime? ActualCloseDate { get; set; }

        /// <summary>Next follow-up date — powers the overdue / at-risk pipeline indicators.</summary>
        [Column("NextActionDate")]
        public DateTime? NextActionDate { get; set; }

        [Column("NextAction")]
        [MaxLength(255)]
        public string? NextAction { get; set; }

        /// <summary>Reason captured when the deal is marked lost (for win/loss analytics).</summary>
        [Column("LostReason")]
        [MaxLength(255)]
        public string? LostReason { get; set; }

        [Column("Category")]
        [MaxLength(50)]
        public string? Category { get; set; }

        [Column("Source")]
        [MaxLength(50)]
        public string? Source { get; set; }

        [Column("Notes")]
        public string? Notes { get; set; }

        [Column("Tags")]
        public string[]? Tags { get; set; }

        [Column("AssignedTo")]
        [MaxLength(50)]
        public string? AssignedTo { get; set; }

        [Column("AssignedToName")]
        [MaxLength(255)]
        public string? AssignedToName { get; set; }

        // ── Conversion tracking ──
        [Column("ConvertedToOfferId")]
        [MaxLength(50)]
        public string? ConvertedToOfferId { get; set; }

        [Column("ConvertedToSaleId")]
        [MaxLength(50)]
        public string? ConvertedToSaleId { get; set; }

        [Column("ConvertedToProjectId")]
        [MaxLength(50)]
        public string? ConvertedToProjectId { get; set; }

        [Column("ConvertedAt")]
        public DateTime? ConvertedAt { get; set; }

        // ── Audit ──
        [Required]
        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [Column("CreatedByName")]
        [MaxLength(255)]
        public string? CreatedByName { get; set; }

        [Column("ModifiedDate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("ModifiedBy")]
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        [Column("LastActivity")]
        public DateTime? LastActivity { get; set; }

        // Navigation
        public virtual ICollection<DealItem>? Items { get; set; }
        public virtual ICollection<DealActivity>? Activities { get; set; }
    }
}
