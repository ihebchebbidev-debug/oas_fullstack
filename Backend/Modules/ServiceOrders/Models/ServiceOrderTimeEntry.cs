using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.ServiceOrders.Models
{
    [ModuleScope("service_orders")]
    [Table("ServiceOrderTimeEntries")]
    public class ServiceOrderTimeEntry : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("ServiceOrderId")]
        public int ServiceOrderId { get; set; }

        [Column("TechnicianId")]
        [MaxLength(50)]
        public string? TechnicianId { get; set; }

        [Required]
        [Column("WorkType")]
        [MaxLength(50)]
        public string WorkType { get; set; } = "work";

        [Required]
        [Column("StartTime")]
        public DateTime StartTime { get; set; }

        [Column("EndTime")]
        public DateTime? EndTime { get; set; }

        [Column("Duration")]
        public int Duration { get; set; } // in minutes

        [Column("Description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("Billable")]
        public bool Billable { get; set; } = true;

        [Column("HourlyRate", TypeName = "decimal(18,2)")]
        public decimal? HourlyRate { get; set; }

        [Column("TotalCost", TypeName = "decimal(18,2)")]
        public decimal? TotalCost { get; set; }

        [Column("Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("InvoiceStatus")]
        [MaxLength(50)]
        public string? InvoiceStatus { get; set; } // null, "selected_for_invoice", "invoiced"

        // Navigation property
        [ForeignKey("ServiceOrderId")]
        public virtual ServiceOrder? ServiceOrder { get; set; }
    }
}
