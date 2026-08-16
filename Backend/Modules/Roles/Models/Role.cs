using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.Users.Models;

namespace MyApi.Modules.Roles.Models
{
    [Table("Roles")]
    public class Role : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = "system";

        public DateTime? ModifiedDate { get; set; }

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        public string? CreatedUser { get; set; } = "system";

        [MaxLength(100)]
        public string? ModifyUser { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
