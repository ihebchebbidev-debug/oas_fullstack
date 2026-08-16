using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Users.Models
{
    /// <summary>
    /// User preferences for regular Users (not MainAdminUsers).
    /// MainAdminUsers use PreferencesJson column directly on MainAdminUsers table.
    /// </summary>
    [Table("UserPreferences")]
    public class UserPreferences : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// JSONB column storing all preferences as JSON
        /// </summary>
        [Required]
        [Column("PreferencesJson", TypeName = "jsonb")]
        public string PreferencesJson { get; set; } = "{}";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property - references Users table, NOT MainAdminUsers
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}
