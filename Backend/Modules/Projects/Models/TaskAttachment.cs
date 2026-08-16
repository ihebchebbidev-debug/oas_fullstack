using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Projects.Models
{
    [ModuleScope("projects")]
    public class TaskAttachment : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int TaskId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string UploadedBy { get; set; } = string.Empty;

        // Navigation property
        [ForeignKey("TaskId")]
        public virtual ProjectTask? ProjectTask { get; set; }
    }
}
