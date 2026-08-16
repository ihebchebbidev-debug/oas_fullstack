using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.Skills.Models;

namespace MyApi.Modules.Roles.Models
{
    [Table("RoleSkills")]
    public class RoleSkill : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        public int RoleId { get; set; }

        [Required]
        public int SkillId { get; set; }

        [Required]
        [MaxLength(100)]
        public string AssignedBy { get; set; } = string.Empty;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation properties
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; } = null!;

        [ForeignKey("SkillId")]
        public virtual Skill Skill { get; set; } = null!;
    }
}
