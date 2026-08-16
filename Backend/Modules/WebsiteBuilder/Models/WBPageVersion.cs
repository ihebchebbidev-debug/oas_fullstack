using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WebsiteBuilder.Models
{
    [Table("WB_PageVersions")]
    public class WBPageVersion : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int PageId { get; set; }

        [Required]
        public int SiteId { get; set; }

        public int VersionNumber { get; set; } = 1;

        [Required]
        [Column("ComponentsJson", TypeName = "jsonb")]
        public string ComponentsJson { get; set; } = "[]";

        [MaxLength(500)]
        public string? ChangeMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = "system";

        // Navigation properties
        [ForeignKey("PageId")]
        public virtual WBPage? Page { get; set; }

        [ForeignKey("SiteId")]
        public virtual WBSite? Site { get; set; }
    }
}
