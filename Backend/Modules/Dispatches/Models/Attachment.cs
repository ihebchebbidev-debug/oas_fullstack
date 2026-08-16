using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Dispatches.Models
{
    [ModuleScope("dispatches")]
    [Table("Attachments")]
    public class Attachment : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DispatchId")]
        public int DispatchId { get; set; }

        [Required]
        [Column("FileName")]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [Column("FilePath")]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [Column("FileSize")]
        public long FileSize { get; set; }

        [Required]
        [Column("ContentType")]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        [Column("AttachmentType")]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [Column("UploadedDate")]
        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("UploadedBy")]
        [MaxLength(100)]
        public string UploadedBy { get; set; } = string.Empty;

        // Optional geotag captured at upload time (mobile field techs).
        // Nullable — desktop uploads have no location.
        [Column("Latitude")]
        public double? Latitude { get; set; }

        [Column("Longitude")]
        public double? Longitude { get; set; }
    }
}
