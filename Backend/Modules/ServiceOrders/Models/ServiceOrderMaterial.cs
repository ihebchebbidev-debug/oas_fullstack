using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.ServiceOrders.Models
{
    /// <summary>
    /// Represents materials directly associated with a service order (from sale conversion or manually added).
    /// This is separate from DispatchMaterials which are used during dispatch execution.
    /// </summary>
    [ModuleScope("service_orders")]
    [Table("ServiceOrderMaterials")]
    public class ServiceOrderMaterial : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("ServiceOrderId")]
        public int ServiceOrderId { get; set; }

        [Column("SaleItemId")]
        public int? SaleItemId { get; set; }

        [Column("ArticleId")]
        public int? ArticleId { get; set; }

        [Required]
        [Column("Name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("Sku")]
        [MaxLength(100)]
        public string? Sku { get; set; }

        [Column("Description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [Column("Quantity", TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        /// <summary>
        /// Planned/estimated quantity for this material line (what was expected).
        /// Distinct from <see cref="Quantity"/>, which represents the delivered/used amount.
        /// Falls back to <see cref="Quantity"/> when not explicitly set (legacy rows).
        /// </summary>
        [Column("EstimatedQuantity", TypeName = "decimal(18,2)")]
        public decimal? EstimatedQuantity { get; set; }

        [Required]
        [Column("UnitPrice", TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column("TotalPrice", TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [Column("Status")]
        [MaxLength(50)]
        public string Status { get; set; } = "pending"; // pending, delivered, used

        [Column("Source")]
        [MaxLength(50)]
        public string Source { get; set; } = "sale_conversion"; // sale_conversion, manual

        [Column("InternalComment")]
        [MaxLength(1000)]
        public string? InternalComment { get; set; }

        [Column("ExternalComment")]
        [MaxLength(1000)]
        public string? ExternalComment { get; set; }

        [Column("Replacing")]
        public bool Replacing { get; set; } = false;

        [Column("OldArticleModel")]
        [MaxLength(255)]
        public string? OldArticleModel { get; set; }

        [Column("OldArticleStatus")]
        [MaxLength(50)]
        public string? OldArticleStatus { get; set; }

        // FK to Installations.Id. INT for cross-layer consistency (see ServiceOrderJob.InstallationId).
        [Column("InstallationId")]
        public int? InstallationId { get; set; }

        [Column("InstallationName")]
        [MaxLength(255)]
        public string? InstallationName { get; set; }

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [Column("InvoiceStatus")]
        [MaxLength(50)]
        public string? InvoiceStatus { get; set; } // null, "selected_for_invoice", "invoiced"

        [Required]
        [Column("Unit")]
        [MaxLength(20)]
        public string Unit { get; set; } = "piece";

        // Navigation property
        [ForeignKey("ServiceOrderId")]
        public virtual ServiceOrder? ServiceOrder { get; set; }
    }
}
