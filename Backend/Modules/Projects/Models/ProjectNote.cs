using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Projects.Models
{
    [ModuleScope("projects")]
    public class ProjectNote : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        [StringLength(5000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(255)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        [StringLength(255)]
        public string? ModifiedBy { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }
    }
}