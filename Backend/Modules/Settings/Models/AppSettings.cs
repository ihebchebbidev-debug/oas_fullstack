using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Settings.Models
{
    /// <summary>
    /// Global application settings - key/value store for company-wide configuration.
    /// Follows the same standalone-table pattern as NumberingSettings and PdfSettings.
    /// </summary>
    [Table("AppSettings")]
    public class AppSettings : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Unique setting identifier, e.g. "JobConversionMode"
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Column("setting_key")]
        public string SettingKey { get; set; } = string.Empty;

        /// <summary>
        /// Setting value stored as text
        /// </summary>
        [Required]
        [Column("setting_value")]
        public string Value { get; set; } = string.Empty;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
