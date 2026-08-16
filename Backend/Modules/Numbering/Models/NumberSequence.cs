using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Numbering.Models
{
    /// <summary>
    /// Atomic counter table for document numbering sequences.
    /// One row per (entity, period) combination.
    /// </summary>
    [Table("NumberSequences")]
    public class NumberSequence : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Entity identifier matching NumberingSettings.EntityName
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("entity_name")]
        public string EntityName { get; set; } = string.Empty;

        /// <summary>
        /// Period key for reset grouping.
        /// Examples: "all" (never reset), "2026" (yearly), "2026-02" (monthly)
        /// </summary>
        [Required]
        [MaxLength(20)]
        [Column("period_key")]
        public string PeriodKey { get; set; } = "all";

        /// <summary>
        /// Last consumed sequence value. Next value = LastValue + 1.
        /// Updated atomically via UPDATE ... SET last_value = last_value + 1 RETURNING last_value.
        /// </summary>
        [Column("last_value")]
        public long LastValue { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
