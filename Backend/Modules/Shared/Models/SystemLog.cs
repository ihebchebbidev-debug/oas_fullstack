using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Shared.Models
{
    [Table("SystemLogs")]
    public class SystemLog : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(20)]
        public string Level { get; set; } = "info"; // info, warning, error, success

        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Module { get; set; } = string.Empty;

        [StringLength(50)]
        public string Action { get; set; } = "other"; // create, read, update, delete, login, logout, export, import, other

        [StringLength(100)]
        public string? UserId { get; set; }

        [StringLength(200)]
        public string? UserName { get; set; }

        [StringLength(100)]
        public string? EntityType { get; set; }

        [StringLength(100)]
        public string? EntityId { get; set; }

        public string? Details { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Metadata { get; set; }
    }
}
