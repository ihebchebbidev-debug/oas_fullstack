using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Projects.Models
{
    [ModuleScope("projects")]
    public class ProjectColumn : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int DisplayOrder { get; set; }

        [StringLength(7)]
        public string? Color { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;
    }
}