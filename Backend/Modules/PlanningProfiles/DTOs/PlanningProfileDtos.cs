using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.PlanningProfiles.DTOs
{
    public class PlanningProfileDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string OwnerUserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public bool IsShared { get; set; }
        public List<string> VisibleUserIds { get; set; } = new();
        public List<string>? RequiredSkillIds { get; set; }
        public object Settings { get; set; } = new { };
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public class CreatePlanningProfileDto
    {
        [Required]
        [StringLength(120, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [StringLength(16)]
        public string? Color { get; set; }

        [StringLength(64)]
        public string? Icon { get; set; }

        public bool IsShared { get; set; }

        [MaxLength(500, ErrorMessage = "Too many visible users (max 500).")]
        public List<string> VisibleUserIds { get; set; } = new();

        [MaxLength(200, ErrorMessage = "Too many required skills (max 200).")]
        public List<string>? RequiredSkillIds { get; set; }

        public object Settings { get; set; } = new { };
    }

    public class UpdatePlanningProfileDto
    {
        [StringLength(120, MinimumLength = 1)]
        public string? Name { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }

        [StringLength(16)]
        public string? Color { get; set; }

        [StringLength(64)]
        public string? Icon { get; set; }

        public bool? IsShared { get; set; }

        [MaxLength(500, ErrorMessage = "Too many visible users (max 500).")]
        public List<string>? VisibleUserIds { get; set; }

        [MaxLength(200, ErrorMessage = "Too many required skills (max 200).")]
        public List<string>? RequiredSkillIds { get; set; }

        public object? Settings { get; set; }
    }
}
