using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WebsiteBuilder.Models
{
    [Table("WB_FormSubmissions")]
    public class WBFormSubmission : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SiteId { get; set; }

        public int? PageId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FormComponentId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string FormLabel { get; set; } = string.Empty;

        [MaxLength(200)]
        public string PageTitle { get; set; } = string.Empty;

        [Required]
        [Column("DataJson", TypeName = "jsonb")]
        public string DataJson { get; set; } = "{}";

        [MaxLength(50)]
        public string? Source { get; set; } = "website";

        [MaxLength(20)]
        public string? WebhookStatus { get; set; }

        [Column(TypeName = "text")]
        public string? WebhookResponse { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        // ── Wave 2: soft-delete (GDPR audit trail) ──
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        [MaxLength(100)]
        public string? DeletedBy { get; set; }

        // Navigation properties
        [ForeignKey("SiteId")]
        public virtual WBSite? Site { get; set; }

        [ForeignKey("PageId")]
        public virtual WBPage? Page { get; set; }
    }
}
