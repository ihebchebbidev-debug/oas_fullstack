using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Notifications.Models
{
    [ModuleScope("notifications")]
    [Table("Notifications")]
    public class Notification : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "info"; // info, warning, success, message

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = "system"; // sale, offer, service_order, task, system

        [MaxLength(255)]
        public string? Link { get; set; }

        public int? RelatedEntityId { get; set; }

        [MaxLength(50)]
        public string? RelatedEntityType { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime? ReadAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
