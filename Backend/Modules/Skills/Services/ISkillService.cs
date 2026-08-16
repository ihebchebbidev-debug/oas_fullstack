using MyApi.Modules.Skills.DTOs;

namespace MyApi.Modules.Skills.Services
{
    public interface ISkillService
    {
        Task<IEnumerable<SkillDto>> GetAllSkillsAsync();
        Task<SkillDto?> GetSkillByIdAsync(int id);
        Task<SkillDto> CreateSkillAsync(CreateSkillRequest request, string createdBy);
        Task<SkillDto> UpdateSkillAsync(int id, UpdateSkillRequest request, string modifiedBy);
        Task<bool> DeleteSkillAsync(int id);
        Task<bool> SkillExistsAsync(string name, int? excludeId = null);
        Task<bool> AssignSkillToUserAsync(int userId, int skillId, string assignedBy, AssignSkillToUserRequest? request = null);
        Task<bool> RemoveSkillFromUserAsync(int userId, int skillId);
        Task<IEnumerable<UserSkillDto>> GetUserSkillsAsync(int userId);
        Task<IEnumerable<SkillDto>> GetSkillsByCategoryAsync(string category);
        
        // Role-Skill assignment methods
        Task<bool> AssignSkillToRoleAsync(int roleId, int skillId, string assignedBy, string? notes = null);
        Task<bool> RemoveSkillFromRoleAsync(int roleId, int skillId);
        Task<IEnumerable<SkillDto>> GetRoleSkillsAsync(int roleId);
    }
}
