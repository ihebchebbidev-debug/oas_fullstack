using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Purchases.Models
{
    [ModuleScope("purchases")]
    [Table("PurchaseActivities")]
    public class PurchaseActivity : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("EntityType")]
        [MaxLength(30)]
        public string EntityType { get; set; } = string.Empty;
        // purchase_order, goods_receipt, supplier_invoice

        [Required]
        [Column("EntityId")]
        public int EntityId { get; set; }

        [Required]
        [Column("ActivityType")]
        [MaxLength(50)]
        public string ActivityType { get; set; } = string.Empty;
        // created, status_changed, sent, paid, received, etc.

        [Column("Description")]
        public string? Description { get; set; }

        [Column("OldValue")]
        [MaxLength(500)]
        public string? OldValue { get; set; }

        [Column("NewValue")]
        [MaxLength(500)]
        public string? NewValue { get; set; }

        [Required]
        [Column("PerformedBy")]
        [MaxLength(100)]
        public string PerformedBy { get; set; } = string.Empty;

        [Column("PerformedByName")]
        [MaxLength(255)]
        public string? PerformedByName { get; set; }

        [Required]
        [Column("PerformedAt")]
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    }
}
