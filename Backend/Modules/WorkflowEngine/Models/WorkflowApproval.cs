using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WorkflowEngine.Models
{
    [ModuleScope("workflow_engine")]
    [Table("WorkflowApprovals")]
    public class WorkflowApproval : ITenantEntity
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
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Message { get; set; }

        [Required]
        [MaxLength(50)]
        public string ApproverRole { get; set; } = "manager"; // 'manager', 'admin', 'dispatcher'

        [MaxLength(100)]
        public string? ApprovedById { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "pending"; // 'pending', 'approved', 'rejected', 'expired'

        [MaxLength(500)]
        public string? ResponseNote { get; set; }

        public int TimeoutHours { get; set; } = 24;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        // Navigation property
        [ForeignKey("ExecutionId")]
        public virtual WorkflowExecution? Execution { get; set; }
    }
}
