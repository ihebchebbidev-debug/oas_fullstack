using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Projects.Models
{
    /// <summary>
    /// TaskComment model matching database schema:
    /// Id, TaskId, Comment, CreatedDate, CreatedBy
    /// </summary>
    [ModuleScope("projects")]
    public class TaskComment : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int TaskId { get; set; }

        [Required]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        // Navigation property
        [ForeignKey("TaskId")]
        public virtual ProjectTask? ProjectTask { get; set; }
    }
}
