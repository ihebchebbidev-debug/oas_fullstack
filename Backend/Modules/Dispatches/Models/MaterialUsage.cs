using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Dispatches.Models
{
    [ModuleScope("dispatches")]
    [Table("MaterialUsage")]
    public class MaterialUsage : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DispatchId")]
        public int DispatchId { get; set; }

        // Which job of a multi-job dispatch this material belongs to (null = whole dispatch / legacy).
        [Column("ServiceOrderJobId")]
        public int? ServiceOrderJobId { get; set; }

        // Denormalized installation the material rolls up to (see TimeEntry.InstallationId).
        [Column("InstallationId")]
        public int? InstallationId { get; set; }

        [Column("ArticleId")]
        public int? ArticleId { get; set; }

        [Required]
        [Column("Description")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column("Quantity", TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        [Required]
        [Column("UnitPrice", TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column("TotalPrice", TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [Required]
        [Column("UsedDate")]
        public DateTime UsedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("RecordedBy")]
        [MaxLength(100)]
        public string RecordedBy { get; set; } = string.Empty;

        [Required]
        [Column("Unit")]
        [MaxLength(20)]
        public string Unit { get; set; } = "piece";

        /// <summary>Set when this material line pushed cumulative actuals beyond the planned material budget.</summary>
        [Column("OverrunFlag")]
        public bool OverrunFlag { get; set; } = false;

        [Column("OverrunReason")]
        [MaxLength(500)]
        public string? OverrunReason { get; set; }

        // --- Approval workflow (mirrors Expenses) ---
        [Column("ApprovalStatus")]
        [MaxLength(20)]
        public string ApprovalStatus { get; set; } = "pending"; // pending | approved | rejected

        [Column("ApprovedBy")]
        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        [Column("ApprovedAt")]
        public DateTime? ApprovedAt { get; set; }

        [Column("RejectionReason")]
        [MaxLength(500)]
        public string? RejectionReason { get; set; }
    }
}
