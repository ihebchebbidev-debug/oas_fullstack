using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WorkflowEngine.Models
{
    /// <summary>
    /// Tracks which entities have been processed by which workflow triggers.
    /// Prevents duplicate workflow executions when using state-based polling.
    /// </summary>
    [ModuleScope("workflow_engine")]
    [Table("WorkflowProcessedEntities")]
    public class WorkflowProcessedEntity : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The trigger ID that processed this entity
        /// </summary>
        [Required]
        public int TriggerId { get; set; }

        /// <summary>
        /// The entity type (offer, sale, service_order, dispatch)
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// The entity ID that was processed
        /// </summary>
        [Required]
        public int EntityId { get; set; }

        /// <summary>
        /// The status the entity was in when it was processed
        /// </summary>
        [MaxLength(50)]
        public string? ProcessedStatus { get; set; }

        /// <summary>
        /// When this entity was processed
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The execution ID created for this processing
        /// </summary>
        public int? ExecutionId { get; set; }

        // Navigation property
        [ForeignKey("TriggerId")]
        public virtual WorkflowTrigger? Trigger { get; set; }
    }
}
