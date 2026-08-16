using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Roles.DTOs;
using MyApi.Modules.Roles.Services;
using MyApi.Modules.Shared.DTOs;

namespace MyApi.Modules.Roles.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PermissionsController : ControllerBase
    {
        private readonly IPermissionService _permissionService;
        private readonly ILogger<PermissionsController> _logger;

        public PermissionsController(IPermissionService permissionService, ILogger<PermissionsController> logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        private string GetCurrentUserIdentity() =>
            User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? User?.FindFirst("UserId")?.Value
            ?? "system";

        /// <summary>
        /// Get all permissions for a specific role
        /// </summary>
        [HttpGet("role/{roleId}")]
        public async Task<ActionResult<ApiResponse<RolePermissionsDto>>> GetRolePermissions(int roleId)
        {
            try
            {
                var permissions = await _permissionService.GetRolePermissionsAsync(roleId);
                return Ok(ApiResponse<RolePermissionsDto>.SuccessResponse(permissions, "Role permissions retrieved successfully"));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ApiResponse<object>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving permissions for role {RoleId}", roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve role permissions"));
            }
        }

        /// <summary>
        /// Update multiple permissions for a role at once
        /// </summary>
        [HttpPut("role/{roleId}")]
        public async Task<ActionResult<ApiResponse<RolePermissionsDto>>> UpdateRolePermissions(
            int roleId, 
            [FromBody] UpdateRolePermissionsRequest request)
        {
            try
            {
                var modifiedBy = GetCurrentUserIdentity();
                var permissions = await _permissionService.UpdateRolePermissionsAsync(roleId, request, modifiedBy);
                return Ok(ApiResponse<RolePermissionsDto>.SuccessResponse(permissions, "Role permissions updated successfully"));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ApiResponse<object>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating permissions for role {RoleId}", roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to update role permissions"));
            }
        }

        /// <summary>
        /// Set a single permission for a role
        /// </summary>
        [HttpPost("role/{roleId}/set")]
        public async Task<ActionResult<ApiResponse<PermissionDto>>> SetPermission(
            int roleId, 
            [FromBody] SetPermissionRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Module) || string.IsNullOrWhiteSpace(request.Action))
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("Module and action are required"));
                }

                var modifiedBy = GetCurrentUserIdentity();
                var permission = await _permissionService.SetPermissionAsync(roleId, request, modifiedBy);
                return Ok(ApiResponse<PermissionDto>.SuccessResponse(permission, "Permission set successfully"));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ApiResponse<object>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting permission for role {RoleId}", roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to set permission"));
            }
        }

        /// <summary>
        /// Delete a specific permission from a role
        /// </summary>
        [HttpDelete("role/{roleId}/{module}/{action}")]
        public async Task<ActionResult<ApiResponse<object>>> DeletePermission(int roleId, string module, string action)
        {
            try
            {
                var success = await _permissionService.DeletePermissionAsync(roleId, module, action);
                if (!success)
                {
                    return NotFound(ApiResponse<object>.ErrorResponse("Permission not found"));
                }
                return Ok(ApiResponse<object>.SuccessResponse(null, "Permission deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting permission for role {RoleId}", roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to delete permission"));
            }
        }

        /// <summary>
        /// Get all permissions for a specific user (aggregated from all their roles)
        /// </summary>
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<ApiResponse<UserPermissionsDto>>> GetUserPermissions(int userId)
        {
            try
            {
                var permissions = await _permissionService.GetUserPermissionsAsync(userId);
                return Ok(ApiResponse<UserPermissionsDto>.SuccessResponse(permissions, "User permissions retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving permissions for user {UserId}", userId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve user permissions"));
            }
        }

        /// <summary>
        /// Check if a user has a specific permission
        /// </summary>
        [HttpPost("user/{userId}/check")]
        public async Task<ActionResult<ApiResponse<CheckPermissionResponse>>> CheckUserPermission(
            int userId, 
            [FromBody] CheckPermissionRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Module) || string.IsNullOrWhiteSpace(request.Action))
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("Module and action are required"));
                }

                var hasPermission = await _permissionService.UserHasPermissionAsync(userId, request.Module, request.Action);
                var response = new CheckPermissionResponse
                {
                    HasPermission = hasPermission,
                    Module = request.Module,
                    Action = request.Action
                };
                return Ok(ApiResponse<CheckPermissionResponse>.SuccessResponse(response, "Permission check completed"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission for user {UserId}", userId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to check permission"));
            }
        }

        /// <summary>
        /// Grant all permissions to a role
        /// </summary>
        [HttpPost("role/{roleId}/grant-all")]
        public async Task<ActionResult<ApiResponse<object>>> GrantAllPermissions(int roleId)
        {
            try
            {
                var modifiedBy = GetCurrentUserIdentity();
                var success = await _permissionService.GrantAllPermissionsAsync(roleId, modifiedBy);
                if (!success)
                {
                    return NotFound(ApiResponse<object>.ErrorResponse("Role not found"));
                }
                return Ok(ApiResponse<object>.SuccessResponse(null, "All permissions granted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting all permissions to role {RoleId}", roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to grant all permissions"));
            }
        }

        /// <summary>
        /// Revoke all permissions from a role
        /// </summary>
        [HttpPost("role/{roleId}/revoke-all")]
        public async Task<ActionResult<ApiResponse<object>>> RevokeAllPermissions(int roleId)
        {
            try
            {
                var modifiedBy = GetCurrentUserIdentity();
                var success = await _permissionService.RevokeAllPermissionsAsync(roleId, modifiedBy);
                return Ok(ApiResponse<object>.SuccessResponse(null, "All permissions revoked successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all permissions from role {RoleId}", roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to revoke all permissions"));
            }
        }

        /// <summary>
        /// Grant all permissions for a specific module to a role
        /// </summary>
        [HttpPost("role/{roleId}/module/{module}/grant")]
        public async Task<ActionResult<ApiResponse<object>>> GrantModulePermissions(int roleId, string module)
        {
            try
            {
                var modifiedBy = GetCurrentUserIdentity();
                var success = await _permissionService.GrantModulePermissionsAsync(roleId, module, modifiedBy);
                if (!success)
                {
                    return NotFound(ApiResponse<object>.ErrorResponse("Role or module not found"));
                }
                return Ok(ApiResponse<object>.SuccessResponse(null, $"All {module} permissions granted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting {Module} permissions to role {RoleId}", module, roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Failed to grant {module} permissions"));
            }
        }

        /// <summary>
        /// Revoke all permissions for a specific module from a role
        /// </summary>
        [HttpPost("role/{roleId}/module/{module}/revoke")]
        public async Task<ActionResult<ApiResponse<object>>> RevokeModulePermissions(int roleId, string module)
        {
            try
            {
                var modifiedBy = GetCurrentUserIdentity();
                var success = await _permissionService.RevokeModulePermissionsAsync(roleId, module, modifiedBy);
                return Ok(ApiResponse<object>.SuccessResponse(null, $"All {module} permissions revoked successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking {Module} permissions from role {RoleId}", module, roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Failed to revoke {module} permissions"));
            }
        }
    }
}
