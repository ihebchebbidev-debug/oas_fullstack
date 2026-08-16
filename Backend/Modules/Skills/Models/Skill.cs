using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.Users.Models;

namespace MyApi.Modules.Skills.Models
{
    [ModuleScope("skills")]
    [Table("Skills")]
    public class Skill : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = "system";

        [MaxLength(20)]
        public string? Level { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        public string? CreatedUser { get; set; } = "system";

        [MaxLength(100)]
        public string? ModifyUser { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        public virtual ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
    }
}
