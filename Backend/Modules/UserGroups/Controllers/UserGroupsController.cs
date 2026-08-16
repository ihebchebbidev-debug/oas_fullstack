using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Shared.DTOs;
using MyApi.Modules.UserGroups.DTOs;
using MyApi.Modules.UserGroups.Services;

namespace MyApi.Modules.UserGroups.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserGroupsController : ControllerBase
    {
        private readonly IUserGroupService _service;
        private readonly ILogger<UserGroupsController> _logger;

        public UserGroupsController(IUserGroupService service, ILogger<UserGroupsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private string GetCurrentUserIdentity() =>
            User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? User?.FindFirst("UserId")?.Value
            ?? "system";

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<UserGroupDto>>>> GetAll()
        {
            try
            {
                var groups = await _service.GetAllAsync();
                return Ok(ApiResponse<IEnumerable<UserGroupDto>>.SuccessResponse(groups, "User groups retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user groups");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve user groups"));
            }
        }

        // Route-ordering: specific routes BEFORE {id} to avoid conflicts.
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<ApiResponse<IEnumerable<UserGroupDto>>>> GetUserGroups(int userId)
        {
            try
            {
                var groups = await _service.GetUserGroupsAsync(userId);
                return Ok(ApiResponse<IEnumerable<UserGroupDto>>.SuccessResponse(groups, "User groups retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving groups for user {UserId}", userId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve user groups"));
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<UserGroupDto>>> GetById(int id)
        {
            try
            {
                var group = await _service.GetByIdAsync(id);
                if (group == null) return NotFound(ApiResponse<object>.ErrorResponse("User group not found"));
                return Ok(ApiResponse<UserGroupDto>.SuccessResponse(group, "User group retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user group {Id}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve user group"));
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserGroupDto>>> Create([FromBody] CreateUserGroupRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(ApiResponse<object>.ErrorResponse("Group name is required"));
                if (await _service.ExistsAsync(request.Name))
                    return BadRequest(ApiResponse<object>.ErrorResponse("A group with this name already exists"));

                var group = await _service.CreateAsync(request, GetCurrentUserIdentity());
                return CreatedAtAction(nameof(GetById), new { id = group.Id },
                    ApiResponse<UserGroupDto>.SuccessResponse(group, "User group created successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user group");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to create user group"));
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<UserGroupDto>>> Update(int id, [FromBody] UpdateUserGroupRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(ApiResponse<object>.ErrorResponse("Group name is required"));
                if (await _service.ExistsAsync(request.Name, id))
                    return BadRequest(ApiResponse<object>.ErrorResponse("A group with this name already exists"));

                var group = await _service.UpdateAsync(id, request, GetCurrentUserIdentity());
                return Ok(ApiResponse<UserGroupDto>.SuccessResponse(group, "User group updated successfully"));
            }
            catch (ArgumentException)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("User group not found"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user group {Id}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to update user group"));
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            try
            {
                var ok = await _service.DeleteAsync(id);
                if (!ok) return NotFound(ApiResponse<object>.ErrorResponse("User group not found"));
                return Ok(ApiResponse<object>.SuccessResponse(null, "User group deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user group {Id}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to delete user group"));
            }
        }

        [HttpGet("{id}/members")]
        public async Task<ActionResult<ApiResponse<IEnumerable<UserGroupMemberDto>>>> GetMembers(int id)
        {
            try
            {
                var members = await _service.GetMembersAsync(id);
                return Ok(ApiResponse<IEnumerable<UserGroupMemberDto>>.SuccessResponse(members, "Members retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving members for group {Id}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve members"));
            }
        }

        [HttpPost("{id}/members")]
        public async Task<ActionResult<ApiResponse<object>>> AssignMembers(int id, [FromBody] AssignUsersToGroupRequest request)
        {
            try
            {
                var added = await _service.AssignUsersAsync(id, request.UserIds ?? new List<int>(), GetCurrentUserIdentity());
                return Ok(ApiResponse<object>.SuccessResponse(new { added }, "Users assigned to group successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning users to group {Id}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to assign users to group"));
            }
        }

        [HttpDelete("{groupId}/members/{userId}")]
        public async Task<ActionResult<ApiResponse<object>>> RemoveMember(int groupId, int userId)
        {
            try
            {
                var ok = await _service.RemoveMemberAsync(groupId, userId);
                if (!ok) return NotFound(ApiResponse<object>.ErrorResponse("Membership not found"));
                return Ok(ApiResponse<object>.SuccessResponse(null, "User removed from group successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserId} from group {GroupId}", userId, groupId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to remove user from group"));
            }
        }
    }
}
