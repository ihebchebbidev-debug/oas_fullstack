using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WebsiteBuilder.Models
{
    [Table("WB_Media")]
    public class WBMedia : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int? SiteId { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string OriginalName { get; set; } = string.Empty;

        /// <summary>
        /// Relative path on disk, e.g. /uploads/wb_uploads/general/20260209_abc123_logo.png
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// URL to serve/download the file, e.g. /api/WBUpload/file/42
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string FileUrl { get; set; } = string.Empty;

        public long FileSize { get; set; } = 0;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = "image/jpeg";

        public int? Width { get; set; }

        public int? Height { get; set; }

        [MaxLength(200)]
        public string? Folder { get; set; }

        [MaxLength(500)]
        public string? AltText { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string UploadedBy { get; set; } = "system";

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        [ForeignKey("SiteId")]
        public virtual WBSite? Site { get; set; }
    }
}