using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WorkflowEngine.Models
{
    [ModuleScope("workflow_engine")]
    [Table("WorkflowTriggers")]
    public class WorkflowTrigger : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        public int WorkflowId { get; set; }

        [Required]
        [MaxLength(50)]
        public string NodeId { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string EntityType { get; set; } = string.Empty; // 'offer', 'sale', 'service_order', 'dispatch'

        [MaxLength(50)]
        public string? FromStatus { get; set; } // NULL means 'any'

        [MaxLength(50)]
        public string? ToStatus { get; set; } // NULL means 'any'

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("WorkflowId")]
        public virtual WorkflowDefinition? Workflow { get; set; }
    }
}
