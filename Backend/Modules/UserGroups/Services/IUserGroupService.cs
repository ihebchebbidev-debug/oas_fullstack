using MyApi.Modules.UserGroups.DTOs;

namespace MyApi.Modules.UserGroups.Services
{
    public interface IUserGroupService
    {
        Task<IEnumerable<UserGroupDto>> GetAllAsync();
        Task<UserGroupDto?> GetByIdAsync(int id);
        Task<UserGroupDto> CreateAsync(CreateUserGroupRequest request, string createdBy);
        Task<UserGroupDto> UpdateAsync(int id, UpdateUserGroupRequest request, string modifiedBy);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(string name, int? excludeId = null);

        Task<IEnumerable<UserGroupMemberDto>> GetMembersAsync(int groupId);
        Task<int> AssignUsersAsync(int groupId, IEnumerable<int> userIds, string assignedBy);
        Task<bool> RemoveMemberAsync(int groupId, int userId);
        Task<IEnumerable<UserGroupDto>> GetUserGroupsAsync(int userId);
    }
}
