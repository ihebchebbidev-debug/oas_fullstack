using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WorkflowEngine.Models
{
    [ModuleScope("workflow_engine")]
    [Table("WorkflowDefinitions")]
    public class WorkflowDefinition : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "jsonb")]
        public string Nodes { get; set; } = "[]";

        [Required]
        [Column(TypeName = "jsonb")]
        public string Edges { get; set; } = "[]";

        public bool IsActive { get; set; } = true;

        public int Version { get; set; } = 1;

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        public virtual ICollection<WorkflowTrigger> Triggers { get; set; } = new List<WorkflowTrigger>();
        public virtual ICollection<WorkflowExecution> Executions { get; set; } = new List<WorkflowExecution>();
    }
}
