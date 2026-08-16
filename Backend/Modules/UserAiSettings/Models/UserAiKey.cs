using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.UserAiSettings.Models
{
    [Table("UserAiKeys")]
    public class UserAiKey : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(20)]
        public string UserType { get; set; } = "MainAdminUser";

        [Required]
        [MaxLength(100)]
        public string Label { get; set; } = "Key";

        [Required]
        public string ApiKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Provider { get; set; } = "openrouter";

        public int Priority { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
