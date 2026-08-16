using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Dispatches.Models
{
    [ModuleScope("dispatches")]
    [Table("TimeEntries")]
    public class TimeEntry : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DispatchId")]
        public int DispatchId { get; set; }

        // Which job of a multi-job dispatch this entry belongs to (null = whole dispatch / legacy).
        [Column("ServiceOrderJobId")]
        public int? ServiceOrderJobId { get; set; }

        // Denormalized installation the entry rolls up to. Set at write time from the
        // parent Dispatch (installation-scoped) or from the linked ServiceOrderJob.
        // Enables per-installation plan-vs-actual roll-ups without traversing joins.
        [Column("InstallationId")]
        public int? InstallationId { get; set; }

        [Required]
        [Column("TechnicianId")]
        public int TechnicianId { get; set; }

        [Required]
        [Column("StartTime")]
        public DateTime StartTime { get; set; }

        [Column("EndTime")]
        public DateTime? EndTime { get; set; }

        // Store duration in minutes. Use larger precision to avoid overflow (e.g., multi-day entries).
        [Column("Duration", TypeName = "decimal(18,2)")]
        public decimal? Duration { get; set; }

        [Required]
        [Column("ActivityType")]
        [MaxLength(50)]
        public string WorkType { get; set; } = "work";

        [Column("Description")]
        public string? Description { get; set; }

        /// <summary>Whether this logged time can be transferred to a sale/invoice.
        /// Defaults to true so legacy rows stay billable.</summary>
        [Column("Billable")]
        public bool Billable { get; set; } = true;

        [Required]
        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>Set when this entry pushed cumulative actuals beyond the planned budget.</summary>
        [Column("OverrunFlag")]
        public bool OverrunFlag { get; set; } = false;

        [Column("OverrunReason")]
        [MaxLength(500)]
        public string? OverrunReason { get; set; }
    }
}
