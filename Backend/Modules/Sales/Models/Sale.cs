using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;
using System.Collections.Generic;
using MyApi.Infrastructure;

namespace MyApi.Modules.Sales.Models
{
    [ModuleScope("sales")]
    [Table("Sales")]
    public class Sale : ITenantEntity, MyApi.Modules.Shared.Models.ISoftDeletable
    {
        public int TenantId { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("SaleNumber")]
        [MaxLength(50)]
        public string SaleNumber { get; set; } = string.Empty;

        [Column("Title")]
        [MaxLength(255)]
        public string? Title { get; set; }

        [Column("Description")]
        public string? Description { get; set; }

        [Required]
        [Column("ContactId")]
        public int ContactId { get; set; }

        [Column("ProjectId")]
        public int? ProjectId { get; set; }

        /// <summary>
        /// True when this Sale represents a "won deal" of a Project
        /// (created by converting an Offer that belonged to that Project).
        /// </summary>
        [Column("IsDeal")]
        public bool IsDeal { get; set; } = false;

        [Column("SaleDate")]
        public DateTime SaleDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("Status", TypeName = "text")]
        public string Status { get; set; } = "created";

        [Column("TotalAmount", TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } = 0;

        [Column("DiscountPercent", TypeName = "decimal(5,2)")]
        public decimal? DiscountPercent { get; set; }

        [Column("DiscountAmount", TypeName = "decimal(18,2)")]
        public decimal? DiscountAmount { get; set; }

        [Column("TaxAmount", TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0;

        [Column("GrandTotal", TypeName = "decimal(18,2)")]
        public decimal GrandTotal { get; set; } = 0;

        [Column("PaymentStatus")]
        [MaxLength(20)]
        public string PaymentStatus { get; set; } = "pending";

        [Column("PaymentMethod")]
        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        [Column("Notes")]
        public string? Notes { get; set; }

        [Column("Currency")]
        [MaxLength(3)]
        public string Currency { get; set; } = "TND";

        [Column("Taxes", TypeName = "decimal(18,2)")]
        public decimal? Taxes { get; set; } = 0;

        [Column("TaxType")]
        [MaxLength(20)]
        public string TaxType { get; set; } = "percentage";

        [Column("Discount", TypeName = "decimal(18,2)")]
        public decimal? Discount { get; set; } = 0;

        [Column("FiscalStamp", TypeName = "decimal(10,3)")]
        public decimal? FiscalStamp { get; set; } = 1.000m;

        [Column("Stage")]
        [MaxLength(50)]
        public string? Stage { get; set; } = "closed";

        [Column("Priority")]
        [MaxLength(20)]
        public string? Priority { get; set; }

        [Column("Category")]
        [MaxLength(50)]
        public string? Category { get; set; }

        [Column("Source")]
        [MaxLength(50)]
        public string? Source { get; set; }

        [Column("BillingAddress")]
        public string? BillingAddress { get; set; }

        [Column("BillingPostalCode")]
        [MaxLength(20)]
        public string? BillingPostalCode { get; set; }

        [Column("BillingCountry")]
        [MaxLength(100)]
        public string? BillingCountry { get; set; }

        [Column("DeliveryAddress")]
        public string? DeliveryAddress { get; set; }

        [Column("DeliveryPostalCode")]
        [MaxLength(20)]
        public string? DeliveryPostalCode { get; set; }

        [Column("DeliveryCountry")]
        [MaxLength(100)]
        public string? DeliveryCountry { get; set; }

        [Column("EstimatedCloseDate")]
        public DateTime? EstimatedCloseDate { get; set; }

        [Column("ActualCloseDate")]
        public DateTime? ActualCloseDate { get; set; }

        [Column("ValidUntil")]
        public DateTime? ValidUntil { get; set; }

        [Column("AssignedTo")]
        [MaxLength(50)]
        public string? AssignedTo { get; set; }

        [Column("AssignedToName")]
        [MaxLength(255)]
        public string? AssignedToName { get; set; }

        [Column("Tags")]
        public string[]? Tags { get; set; }

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

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [Column("LastActivity")]
        public DateTime? LastActivity { get; set; }

        [Column("OfferId")]
        [MaxLength(50)]
        public string? OfferId { get; set; }

        [Column("ConvertedFromOfferAt")]
        public DateTime? ConvertedFromOfferAt { get; set; }

        /// <summary>
        /// True when this Sale was generated automatically by completing a
        /// direct ServiceOrder (Origin = "direct"). Surfaced in the Sales UI
        /// as an "Auto-generated" chip.
        /// </summary>
        [Column("IsAutoGenerated")]
        public bool IsAutoGenerated { get; set; } = false;

        /// <summary>
        /// FK back to the ServiceOrder that produced this Sale via the
        /// ShadowSaleGenerator. Null for normal sales.
        /// </summary>
        [Column("SourceServiceOrderId")]
        public int? SourceServiceOrderId { get; set; }

        [Column("LostReason")]
        public string? LostReason { get; set; }

        [Column("MaterialsFulfillment")]
        [MaxLength(20)]
        public string? MaterialsFulfillment { get; set; }

        [Column("ServiceOrdersStatus")]
        [MaxLength(20)]
        public string? ServiceOrdersStatus { get; set; }

        // Contact geolocation (copied from contact for map display)
        [Column("ContactLatitude", TypeName = "decimal(10,7)")]
        public decimal? ContactLatitude { get; set; }

        [Column("ContactLongitude", TypeName = "decimal(10,7)")]
        public decimal? ContactLongitude { get; set; }

        [Column("ContactHasLocation")]
        public int ContactHasLocation { get; set; } = 0;

        // Navigation Properties
        public virtual ICollection<SaleItem>? Items { get; set; }
        public virtual ICollection<SaleActivity>? Activities { get; set; }
    }
}
