using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.ServiceOrders.Models
{
    [ModuleScope("service_orders")]
    [Table("ServiceOrderJobs")]
    public class ServiceOrderJob : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("ServiceOrderId")]
        public int ServiceOrderId { get; set; }

        [Column("JobDescription")]
        public string? JobDescription { get; set; }

        [Column("AssignedTechnicianId")]
        public int? AssignedTechnicianId { get; set; }

        [Required]
        [Column("Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "unscheduled";

        [Column("EstimatedHours", TypeName = "decimal(5,2)")]
        public decimal? EstimatedHours { get; set; }

        [Column("ActualHours", TypeName = "decimal(5,2)")]
        public decimal? ActualHours { get; set; }

        [Column("CompletedDate")]
        public DateTime? CompletedDate { get; set; }

        [Column("Title")]
        [MaxLength(255)]
        public string? Title { get; set; }

        [Column("Description")]
        public string? Description { get; set; }

        [Column("SaleItemId")]
        [MaxLength(50)]
        public string? SaleItemId { get; set; }

        // FK to Installations.Id. Stored as INT to match every other layer
        // (Installation.Id, Dispatch.InstallationId, and the new denormalized
        // InstallationId on TimeEntries/Expenses/MaterialUsage).
        [Column("InstallationId")]
        public int? InstallationId { get; set; }

        [Column("InstallationName")]
        [MaxLength(255)]
        public string? InstallationName { get; set; }

        [Column("WorkType")]
        [MaxLength(50)]
        public string? WorkType { get; set; }

        [Column("Priority")]
        [MaxLength(20)]
        public string? Priority { get; set; } = "medium";

        [Column("ScheduledDate")]
        public DateTime? ScheduledDate { get; set; }

        [Column("EstimatedDuration")]
        public int? EstimatedDuration { get; set; }

        [Column("EstimatedCost", TypeName = "decimal(18,2)")]
        public decimal? EstimatedCost { get; set; } = 0;

        [Column("ActualDuration")]
        public int? ActualDuration { get; set; }

        [Column("ActualCost", TypeName = "decimal(18,2)")]
        public decimal? ActualCost { get; set; } = 0;

        [Column("CompletionPercentage")]
        public int? CompletionPercentage { get; set; } = 0;

        [Column("AssignedTechnicianIds")]
        public string[]? AssignedTechnicianIds { get; set; }

        [Column("RequiredSkills")]
        public string[]? RequiredSkills { get; set; }

        [Column("Notes")]
        public string? Notes { get; set; }

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        [ForeignKey("ServiceOrderId")]
        public virtual ServiceOrder? ServiceOrder { get; set; }
    }
}
