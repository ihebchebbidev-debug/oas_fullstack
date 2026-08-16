using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.Skills.Models;

namespace MyApi.Modules.Users.Models
{
    [Table("UserSkills")]
    public class UserSkill : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int SkillId { get; set; }

        [MaxLength(20)]
        public string? ProficiencyLevel { get; set; } // beginner, intermediate, advanced, expert

        public int? YearsOfExperience { get; set; }

        [MaxLength(500)]
        public string? Certifications { get; set; } // JSON string for certifications array

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(100)]
        public string AssignedBy { get; set; } = string.Empty;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation properties - explicitly configured in UserSkillConfiguration
        public virtual User? User { get; set; }

        public virtual Skill? Skill { get; set; }
    }
}
