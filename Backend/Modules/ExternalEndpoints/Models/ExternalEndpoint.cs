using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.ExternalEndpoints.Models
{
    [Table("ExternalEndpoints")]
    public class ExternalEndpoint : ITenantEntity, MyApi.Modules.Shared.Models.ISoftDeletable
    {
        public int TenantId { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("Name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("Slug")]
        [MaxLength(100)]
        public string Slug { get; set; } = string.Empty;

        [Column("Description")]
        public string? Description { get; set; }

        [Required]
        [Column("ApiKey")]
        [MaxLength(128)]
        public string ApiKey { get; set; } = string.Empty;

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("AllowedMethods")]
        [MaxLength(50)]
        public string AllowedMethods { get; set; } = "POST";

        [Column("AllowedOrigins")]
        public string? AllowedOrigins { get; set; }

        [Column("ExpectedSchema")]
        public string? ExpectedSchema { get; set; }

        [Column("ResponseTemplate")]
        public string? ResponseTemplate { get; set; }

        [Column("WebhookForwardUrl")]
        public string? WebhookForwardUrl { get; set; }

        /// <summary>HMAC-SHA256 secret used to sign outbound webhook forwards
        /// (X-Signature header). Auto-generated on create.</summary>
        [Column("ForwardSecret")]
        [MaxLength(128)]
        public string? ForwardSecret { get; set; }

        /// <summary>Days to retain inbound logs (0 = forever).</summary>
        [Column("LogRetentionDays")]
        public int LogRetentionDays { get; set; } = 30;

        /// <summary>Comma-separated accepted content types: "json", "xml", "form", "any".
        /// Empty / "any" means all content types are accepted. Unknown types fall through.</summary>
        [Column("AcceptedContentTypes")]
        [MaxLength(100)]
        public string AcceptedContentTypes { get; set; } = "any";

        [Required]
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        // Navigation
        public virtual ICollection<ExternalEndpointLog>? Logs { get; set; }
    }
}
