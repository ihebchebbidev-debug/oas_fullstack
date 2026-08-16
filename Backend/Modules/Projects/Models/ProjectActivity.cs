using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Projects.Models
{
    [ModuleScope("projects")]
    public class ProjectActivity : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        [StringLength(50)]
        public string ActionType { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Details { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(255)]
        public string CreatedBy { get; set; } = string.Empty;

        public int? RelatedEntityId { get; set; }

        [StringLength(100)]
        public string? RelatedEntityType { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }
    }
}