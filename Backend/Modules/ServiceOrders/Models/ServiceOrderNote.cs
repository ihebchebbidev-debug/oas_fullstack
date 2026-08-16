using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.ServiceOrders.Models
{
    [ModuleScope("service_orders")]
    [Table("ServiceOrderNotes")]
    public class ServiceOrderNote : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("ServiceOrderId")]
        public int ServiceOrderId { get; set; }

        [Required]
        [Column("Content")]
        public string Content { get; set; } = string.Empty;

        // Note type / activity category. Widened from 20 → 50 because activity
        // propagation sends values like "dispatch_status_changed" (23 chars) that
        // overflowed the original varchar(20) and threw 22001 on insert.
        [Column("Type")]
        [MaxLength(50)]
        public string Type { get; set; } = "internal";

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [Column("CreatedByName")]
        [MaxLength(255)]
        public string? CreatedByName { get; set; }

        [Required]
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        [ForeignKey("ServiceOrderId")]
        public virtual ServiceOrder? ServiceOrder { get; set; }
    }
}
