using MyApi.Modules.Roles.DTOs;
using MyApi.Modules.Roles.Models;

namespace MyApi.Modules.Roles.Services
{
    public interface IRoleService
    {
        Task<IEnumerable<RoleDto>> GetAllRolesAsync();
        Task<RoleDto?> GetRoleByIdAsync(int id);
        Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, string createdBy);
        Task<RoleDto> UpdateRoleAsync(int id, UpdateRoleRequest request, string modifiedBy);
        Task<bool> DeleteRoleAsync(int id);
        Task<bool> RoleExistsAsync(string name, int? excludeId = null);
        Task<bool> AssignRoleToUserAsync(int userId, int roleId, string assignedBy);
        Task<bool> RemoveRoleFromUserAsync(int userId, int roleId);
        Task<IEnumerable<RoleDto>> GetUserRolesAsync(int userId);
        Task<Dictionary<string, IEnumerable<RoleDto>>> GetAllUserRolesAsync();
    }
}
