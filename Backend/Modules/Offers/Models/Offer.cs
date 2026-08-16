using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Offers.Models
{
    [ModuleScope("offers")]
    [Table("Offers")]
    public class Offer : ITenantEntity, MyApi.Modules.Shared.Models.ISoftDeletable
    {
        public int TenantId { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("OfferNumber")]
        [MaxLength(50)]
        public string OfferNumber { get; set; } = string.Empty;

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

        [Column("OfferDate")]
        public DateTime OfferDate { get; set; } = DateTime.UtcNow;

        [Column("ValidUntil")]
        public DateTime? ValidUntil { get; set; }

        [Required]
        [Column("Status", TypeName = "text")]
        public string Status { get; set; } = "draft";

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

        [Column("Notes")]
        public string? Notes { get; set; }

        [Column("TermsAndConditions")]
        public string? TermsAndConditions { get; set; }

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

        [Column("DiscountType")]
        [MaxLength(20)]
        public string DiscountType { get; set; } = "percentage";

        [Column("FiscalStamp", TypeName = "decimal(10,3)")]
        public decimal? FiscalStamp { get; set; } = 1.000m;

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

        [Column("ConvertedToSaleId")]
        [MaxLength(50)]
        public string? ConvertedToSaleId { get; set; }

        [Column("ConvertedToServiceOrderId")]
        [MaxLength(50)]
        public string? ConvertedToServiceOrderId { get; set; }

        [Column("ConvertedAt")]
        public DateTime? ConvertedAt { get; set; }

        // Phase A (A3): timestamp for the "accept without convert" branch of
        // ConvertOfferAsync. Together with ConvertedToSaleId this lets the
        // idempotency guard reject a second accept call whether or not a sale
        // was ever created, without falsifying conversion analytics.
        [Column("AcceptedWithoutConversionAt")]
        public DateTime? AcceptedWithoutConversionAt { get; set; }

        [Column("SentCount")]
        public int SentCount { get; set; } = 0;

        // Contact geolocation (copied from contact for map display)
        [Column("ContactLatitude", TypeName = "decimal(10,7)")]
        public decimal? ContactLatitude { get; set; }

        [Column("ContactLongitude", TypeName = "decimal(10,7)")]
        public decimal? ContactLongitude { get; set; }

        [Column("ContactHasLocation")]
        public int ContactHasLocation { get; set; } = 0;

        // Navigation Properties
        public virtual ICollection<OfferItem>? Items { get; set; }
        public virtual ICollection<OfferActivity>? Activities { get; set; }
    }
}
