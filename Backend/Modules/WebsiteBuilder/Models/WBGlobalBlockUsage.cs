using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WebsiteBuilder.Models
{
    [Table("WB_GlobalBlockUsages")]
    public class WBGlobalBlockUsage : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int GlobalBlockId { get; set; }

        [Required]
        public int SiteId { get; set; }

        public int? PageId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("GlobalBlockId")]
        public virtual WBGlobalBlock? GlobalBlock { get; set; }

        [ForeignKey("SiteId")]
        public virtual WBSite? Site { get; set; }

        [ForeignKey("PageId")]
        public virtual WBPage? Page { get; set; }
    }
}
