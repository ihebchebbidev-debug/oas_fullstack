using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Numbering.Models
{
    /// <summary>
    /// Stores configurable numbering templates per entity type (Offer, Sale, ServiceOrder, Dispatch).
    /// </summary>
    [Table("NumberingSettings")]
    public class NumberingSettings : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Entity identifier: 'Offer', 'Sale', 'ServiceOrder', 'Dispatch'
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("entity_name")]
        public string EntityName { get; set; } = string.Empty;

        /// <summary>
        /// Whether this custom numbering is enabled. If false, falls back to legacy logic.
        /// </summary>
        [Column("is_enabled")]
        public bool IsEnabled { get; set; } = false;

        /// <summary>
        /// Template string using tokens: {YYYY}, {YY}, {DATE:format}, {SEQ}, {SEQ:N}, {GUID}, {GUID:N}, {TS:format}, {ENTITY}, {ID}
        /// Example: "OFR-{YEAR}-{SEQ:6}"
        /// </summary>
        [Required]
        [MaxLength(200)]
        [Column("template")]
        public string Template { get; set; } = string.Empty;

        /// <summary>
        /// Generation strategy: 'db_sequence', 'atomic_counter', 'timestamp_random', 'guid'
        /// </summary>
        [Required]
        [MaxLength(30)]
        [Column("strategy")]
        public string Strategy { get; set; } = "atomic_counter";

        /// <summary>
        /// Sequence reset frequency: 'never', 'yearly', 'monthly'
        /// </summary>
        [MaxLength(20)]
        [Column("reset_frequency")]
        public string ResetFrequency { get; set; } = "yearly";

        /// <summary>
        /// Starting value for sequences
        /// </summary>
        [Column("start_value")]
        public int StartValue { get; set; } = 1;

        /// <summary>
        /// Zero-padding digits for sequences (e.g. 6 → 000001)
        /// </summary>
        [Column("padding")]
        public int Padding { get; set; } = 6;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
