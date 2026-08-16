using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Deals.Models
{
    /// <summary>Timeline entry on a deal — note, call, email, meeting, stage change, etc.</summary>
    [ModuleScope("deals")]
    [Table("DealActivities")]
    public class DealActivity : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DealId")]
        public int DealId { get; set; }

        [Required]
        [Column("Type")]
        [MaxLength(30)]
        public string Type { get; set; } = "note"; // note|call|email|meeting|status_change|created|converted

        [Column("Description")]
        public string Description { get; set; } = string.Empty;

        [Column("Details")]
        public string? Details { get; set; }

        [Column("OldValue")]
        [MaxLength(100)]
        public string? OldValue { get; set; }

        [Column("NewValue")]
        [MaxLength(100)]
        public string? NewValue { get; set; }

        [Required]
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [Column("CreatedByName")]
        [MaxLength(255)]
        public string? CreatedByName { get; set; }

        // Navigation
        public virtual Deal? Deal { get; set; }
    }
}
