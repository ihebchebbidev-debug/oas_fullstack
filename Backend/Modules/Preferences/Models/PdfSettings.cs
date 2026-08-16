using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Preferences.Models
{
    /// <summary>
    /// Global PDF Settings - applies to the entire application (not per-user)
    /// </summary>
    [Table("PdfSettings")]
    public class PdfSettings : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Module identifier: 'offers', 'sales', 'dispatches', 'serviceOrders'
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Module { get; set; } = string.Empty;

        /// <summary>
        /// JSONB column storing all PDF settings as JSON
        /// </summary>
        [Required]
        [Column("SettingsJson", TypeName = "jsonb")]
        public string SettingsJson { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
