using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.ServiceOrders.Models
{
    [ModuleScope("service_orders")]
    [Table("ServiceOrderExpenses")]
    public class ServiceOrderExpense : ITenantEntity
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
        [Column("Type")]
        [MaxLength(50)]
        public string Type { get; set; } = "other";

        [Required]
        [Column("Amount", TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column("Currency")]
        [MaxLength(10)]
        public string Currency { get; set; } = "TND";

        [Column("Description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("Date")]
        public DateTime? Date { get; set; }

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
