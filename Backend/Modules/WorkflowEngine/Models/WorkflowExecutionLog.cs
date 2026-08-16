using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WorkflowEngine.Models
{
    [ModuleScope("workflow_engine")]
    [Table("WorkflowExecutionLogs")]
    public class WorkflowExecutionLog : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExecutionId { get; set; }

        [Required]
        [MaxLength(50)]
        public string NodeId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string NodeType { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = string.Empty; // 'started', 'completed', 'failed', 'skipped'

        [Column(TypeName = "jsonb")]
        public string? Input { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Output { get; set; }

        // OBS-3: stack traces and remote API error bodies routinely exceed 500 chars.
        // Use unbounded text so we keep the full error for diagnostics.
        [Column(TypeName = "text")]
        public string? Error { get; set; }

        public int? Duration { get; set; } // milliseconds

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("ExecutionId")]
        public virtual WorkflowExecution? Execution { get; set; }
    }
}
