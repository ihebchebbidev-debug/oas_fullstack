using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WebsiteBuilder.Models
{
    [Table("WB_ActivityLog")]
    public class WBActivityLog : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SiteId { get; set; }

        public int? PageId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = "system";

        // Navigation properties
        [ForeignKey("SiteId")]
        public virtual WBSite? Site { get; set; }
    }
}
