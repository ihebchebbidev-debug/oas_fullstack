using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.UserAiSettings.Models
{
    [Table("UserAiPreferences")]
    public class UserAiPreference : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(20)]
        public string UserType { get; set; } = "MainAdminUser";

        [MaxLength(200)]
        public string? DefaultModel { get; set; }

        [MaxLength(200)]
        public string? FallbackModel { get; set; }

        [Column(TypeName = "decimal(3,2)")]
        public decimal DefaultTemperature { get; set; } = 0.70m;

        public int DefaultMaxTokens { get; set; } = 1000;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
