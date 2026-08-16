using MyApi.Modules.Roles.DTOs;

namespace MyApi.Modules.Roles.Services
{
    public interface IPermissionService
    {
        // Role Permissions CRUD
        Task<RolePermissionsDto> GetRolePermissionsAsync(int roleId);
        Task<RolePermissionsDto> UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsRequest request, string modifiedBy);
        Task<PermissionDto> SetPermissionAsync(int roleId, SetPermissionRequest request, string modifiedBy);
        Task<bool> DeletePermissionAsync(int roleId, string module, string action);
        
        // User Permissions
        Task<UserPermissionsDto> GetUserPermissionsAsync(int userId);
        Task<bool> UserHasPermissionAsync(int userId, string module, string action);
        
        // Bulk Operations
        Task<bool> GrantAllPermissionsAsync(int roleId, string modifiedBy);
        Task<bool> RevokeAllPermissionsAsync(int roleId, string modifiedBy);
        Task<bool> GrantModulePermissionsAsync(int roleId, string module, string modifiedBy);
        Task<bool> RevokeModulePermissionsAsync(int roleId, string module, string modifiedBy);
    }
}
