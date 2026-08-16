using System.Collections.Generic;
using System.Threading.Tasks;
using MyApi.Modules.PlanningProfiles.DTOs;

namespace MyApi.Modules.PlanningProfiles.Services
{
    public interface IPlanningProfileService
    {
        Task<List<PlanningProfileDto>> ListAsync(string currentUserId);
        Task<PlanningProfileDto?> GetByIdAsync(int id, string currentUserId);
        Task<PlanningProfileDto> CreateAsync(CreatePlanningProfileDto dto, string currentUserId);
        Task<PlanningProfileDto> UpdateAsync(int id, UpdatePlanningProfileDto dto, string currentUserId);
        Task DeleteAsync(int id, string currentUserId);
        Task<PlanningProfileDto?> GetActiveAsync(string currentUserId);
        Task SetActiveAsync(int profileId, string currentUserId);
    }
}
