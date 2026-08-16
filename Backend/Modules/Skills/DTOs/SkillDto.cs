namespace MyApi.Modules.Skills.DTOs
{
    public class SkillDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public int UserCount { get; set; }
    }

    public class CreateSkillRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
    }

    public class UpdateSkillRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UserSkillDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SkillId { get; set; }
        public string SkillName { get; set; } = string.Empty;
        public string? SkillCategory { get; set; }
        public string? ProficiencyLevel { get; set; }
        public int? YearsOfExperience { get; set; }
        public string[]? Certifications { get; set; }
        public string? Notes { get; set; }
        public DateTime AssignedAt { get; set; }
    }

    public class AssignSkillToUserRequest
    {
        public string? ProficiencyLevel { get; set; }
        public int? YearsOfExperience { get; set; }
        public string[]? Certifications { get; set; }
        public string? Notes { get; set; }
    }

    public class AssignSkillToRoleRequest
    {
        public string? Notes { get; set; }
    }
}
