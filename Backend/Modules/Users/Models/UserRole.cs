using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.Roles.Models;

namespace MyApi.Modules.Users.Models
{
    [Table("UserRoles")]
    public class UserRole : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int RoleId { get; set; }

        [Required]
        [MaxLength(100)]
        public string AssignedBy { get; set; } = string.Empty;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation properties - explicitly configured in UserRoleConfiguration
        public virtual User? User { get; set; }

        public virtual Role? Role { get; set; }
    }
}
