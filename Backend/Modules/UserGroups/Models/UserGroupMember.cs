using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.UserGroups.Models
{
    [Table("UserGroupMembers")]
    public class UserGroupMember : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int GroupId { get; set; }

        [Required]
        public int UserId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? AssignedBy { get; set; }

        // Navigation
        public virtual UserGroup? Group { get; set; }
    }
}
