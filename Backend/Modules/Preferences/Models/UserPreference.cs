using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Preferences.Models
{
    [Table("UserPreferences")]
    public class UserPreference : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [Column(TypeName = "jsonb")]
        public string PreferencesJson { get; set; } = "{}";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}