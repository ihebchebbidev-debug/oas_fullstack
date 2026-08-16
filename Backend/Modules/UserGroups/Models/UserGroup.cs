using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.UserGroups.Models
{
    [Table("UserGroups")]
    public class UserGroup : ITenantEntity
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

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? CreatedBy { get; set; } = "system";

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        // Navigation
        public virtual ICollection<UserGroupMember> Members { get; set; } = new List<UserGroupMember>();
    }
}
