using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Dispatches.Models
{
    /// <summary>
    /// Dedicated audit record for security- and business-critical dispatch events
    /// (currently cancellations). Written in addition to human-readable notes so
    /// the trail cannot be edited/deleted through the notes UI.
    /// </summary>
    [ModuleScope("dispatches")]
    [Table("DispatchAuditLogs")]
    public class DispatchAuditLog : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DispatchId")]
        public int DispatchId { get; set; }

        [Column("DispatchNumber")]
        [MaxLength(100)]
        public string? DispatchNumber { get; set; }

        // e.g. "cancelled"
        [Required]
        [Column("EventType")]
        [MaxLength(60)]
        public string EventType { get; set; } = string.Empty;

        [Column("OldStatus")]
        [MaxLength(60)]
        public string? OldStatus { get; set; }

        [Column("NewStatus")]
        [MaxLength(60)]
        public string? NewStatus { get; set; }

        [Column("Reason")]
        [MaxLength(1000)]
        public string? Reason { get; set; }

        // Related entities (nullable — captured at the time of the event)
        [Column("ServiceOrderId")]
        public int? ServiceOrderId { get; set; }

        [Column("SaleId")]
        [MaxLength(100)]
        public string? SaleId { get; set; }

        [Column("OfferId")]
        [MaxLength(100)]
        public string? OfferId { get; set; }

        // Actor
        [Column("ActorUserId")]
        [MaxLength(100)]
        public string? ActorUserId { get; set; }

        [Column("ActorName")]
        [MaxLength(200)]
        public string? ActorName { get; set; }

        [Required]
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}