using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Invoices.Models
{
    /// <summary>
    /// Audit-trail entry for a customer invoice. One row per meaningful lifecycle
    /// event (create, update, post, void, delete, status auto-transitions).
    /// The <see cref="Type"/> field is a stable machine code so the UI can
    /// translate it; <see cref="Description"/> is a human-readable fallback.
    /// </summary>
    [ModuleScope("invoices")]
    [Table("InvoiceActivities")]
    public class InvoiceActivity : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("InvoiceId")]
        public int InvoiceId { get; set; }

        [Required]
        [Column("ActivityType")]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [Column("Description")]
        [MaxLength(1000)]
        public string? Description { get; set; }

        [Column("OldValue")]
        [MaxLength(500)]
        public string? OldValue { get; set; }

        [Column("NewValue")]
        [MaxLength(500)]
        public string? NewValue { get; set; }

        [Required]
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [ForeignKey("InvoiceId")]
        public virtual Invoice? Invoice { get; set; }
    }
}
