using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.Contacts.Models;

namespace MyApi.Modules.Dispatches.Models
{
    [ModuleScope("dispatches")]
    [Table("Dispatches")]
    public class Dispatch : ITenantEntity, MyApi.Modules.Shared.Models.ISoftDeletable
    {
        public int TenantId { get; set; }
        
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DispatchNumber")]
        [MaxLength(50)]
        public string DispatchNumber { get; set; } = string.Empty;

        [Required]
        [Column("ContactId")]
        public int ContactId { get; set; }

        [Column("ProjectId")]
        public int? ProjectId { get; set; }

        [Column("ServiceOrderId")]
        public int? ServiceOrderId { get; set; }

        [Column("ProjectTaskId")]
        public int? ProjectTaskId { get; set; }

        [Required]
        [Column("ScheduledDate")]
        public DateTime ScheduledDate { get; set; }

        [Column("CompletedDate")]
        public DateTime? CompletedDate { get; set; }

        [Column("ScheduledStartTime")]
        public TimeSpan? ScheduledStartTime { get; set; }

        [Column("ScheduledEndTime")]
        public TimeSpan? ScheduledEndTime { get; set; }

        [Required]
        [Column("Status", TypeName = "text")]
        public string Status { get; set; } = "pending";

        [Required]
        [Column("Priority")]
        [MaxLength(20)]
        public string Priority { get; set; } = "medium";

        [Column("Description")]
        public string? Description { get; set; }

        [Required]
        [Column("SiteAddress")]
        [MaxLength(500)]
        public string SiteAddress { get; set; } = string.Empty;

        // JobId stored as varchar in DB
        [Column("JobId")]
        [MaxLength(50)]
        public string? JobId { get; set; }

        [Column("DispatchedBy")]
        [MaxLength(50)]
        public string? DispatchedBy { get; set; }

        [Column("DispatchedAt")]
        public DateTime? DispatchedAt { get; set; }

        [Column("CompletionPercentage")]
        public int CompletionPercentage { get; set; } = 0;

        [Column("RequiredSkills")]
        public string[]? RequiredSkills { get; set; }

        [Column("WorkLocationJson", TypeName = "jsonb")]
        public string? WorkLocationJson { get; set; }

        [Column("ActualStartTime")]
        public DateTime? ActualStartTime { get; set; }

        [Column("ActualEndTime")]
        public DateTime? ActualEndTime { get; set; }

        [Column("ActualDuration")]
        public int? ActualDuration { get; set; }

        [Column("IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        [Required]
        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [Column("ModifiedDate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("ModifiedBy")]
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        [Column("InstallationId")]
        public int? InstallationId { get; set; }

        [Column("InstallationName")]
        [MaxLength(255)]
        public string? InstallationName { get; set; }

        // Navigation properties
        [ForeignKey("ContactId")]
        public virtual Contact? Contact { get; set; }
        public virtual List<DispatchTechnician> AssignedTechnicians { get; set; } = new();
        public virtual List<DispatchJob> DispatchJobs { get; set; } = new();
        public virtual List<TimeEntry> TimeEntries { get; set; } = new();
        public virtual List<Expense> Expenses { get; set; } = new();
        public virtual List<MaterialUsage> MaterialsUsed { get; set; } = new();
        public virtual List<Attachment> Attachments { get; set; } = new();
        public virtual List<Note> Notes { get; set; } = new();
    }
}
