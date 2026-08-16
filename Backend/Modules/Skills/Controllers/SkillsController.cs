using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Skills.DTOs;
using MyApi.Modules.Skills.Services;
using MyApi.Modules.Shared.DTOs;

namespace MyApi.Modules.Skills.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SkillsController : ControllerBase
    {
        private readonly ISkillService _skillService;
        private readonly ILogger<SkillsController> _logger;

        public SkillsController(ISkillService skillService, ILogger<SkillsController> logger)
        {
            _skillService = skillService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<SkillDto>>>> GetAllSkills()
        {
            try
            {
                var skills = await _skillService.GetAllSkillsAsync();
                return Ok(ApiResponse<IEnumerable<SkillDto>>.SuccessResponse(skills, "Skills retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving skills");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve skills"));
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<SkillDto>>> GetSkill(int id)
        {
            try
            {
                var skill = await _skillService.GetSkillByIdAsync(id);
                if (skill == null)
                {
                    return NotFound(ApiResponse<object>.ErrorResponse("Skill not found"));
                }

                return Ok(ApiResponse<SkillDto>.SuccessResponse(skill, "Skill retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving skill {SkillId}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve skill"));
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<SkillDto>>> CreateSkill([FromBody] CreateSkillRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("Skill name is required"));
                }

                if (string.IsNullOrWhiteSpace(request.Category))
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("Skill category is required"));
                }

                if (await _skillService.SkillExistsAsync(request.Name))
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("A skill with this name already exists"));
                }

                var createdBy = "System"; // TODO: Get from authenticated user
                var skill = await _skillService.CreateSkillAsync(request, createdBy);

                return CreatedAtAction(nameof(GetSkill), new { id = skill.Id }, 
                    ApiResponse<SkillDto>.SuccessResponse(skill, "Skill created successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating skill");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to create skill"));
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<SkillDto>>> UpdateSkill(int id, [FromBody] UpdateSkillRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("Skill name is required"));
                }

                if (string.IsNullOrWhiteSpace(request.Category))
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("Skill category is required"));
                }

                if (await _skillService.SkillExistsAsync(request.Name, id))
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("A skill with this name already exists"));
                }

                var modifiedBy = "System"; // TODO: Get from authenticated user
                var skill = await _skillService.UpdateSkillAsync(id, request, modifiedBy);

                return Ok(ApiResponse<SkillDto>.SuccessResponse(skill, "Skill updated successfully"));
            }
            catch (ArgumentException)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("Skill not found"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating skill {SkillId}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to update skill"));
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteSkill(int id)
        {
            try
            {
                var success = await _skillService.DeleteSkillAsync(id);
                if (!success)
                {
                    return NotFound(ApiResponse<object>.ErrorResponse("Skill not found"));
                }

                return Ok(ApiResponse<object>.SuccessResponse(null, "Skill deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting skill {SkillId}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to delete skill"));
            }
        }

        [HttpGet("category/{category}")]
        public async Task<ActionResult<ApiResponse<IEnumerable<SkillDto>>>> GetSkillsByCategory(string category)
        {
            try
            {
                var skills = await _skillService.GetSkillsByCategoryAsync(category);
                return Ok(ApiResponse<IEnumerable<SkillDto>>.SuccessResponse(skills, "Skills retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving skills for category {Category}", category);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve skills"));
            }
        }

        [HttpPost("{skillId}/assign/{userId}")]
        public async Task<ActionResult<ApiResponse<object>>> AssignSkillToUser(int skillId, int userId, [FromBody] AssignSkillToUserRequest? request = null)
        {
            try
            {
                var assignedBy = "System"; // TODO: Get from authenticated user
                var success = await _skillService.AssignSkillToUserAsync(userId, skillId, assignedBy, request);
                
                return Ok(ApiResponse<object>.SuccessResponse(null, "Skill assigned to user successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning skill {SkillId} to user {UserId}", skillId, userId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to assign skill to user"));
            }
        }

        [HttpDelete("{skillId}/remove/{userId}")]
        public async Task<ActionResult<ApiResponse<object>>> RemoveSkillFromUser(int skillId, int userId)
        {
            try
            {
                var success = await _skillService.RemoveSkillFromUserAsync(userId, skillId);
                if (!success)
                {
                    return NotFound(ApiResponse<object>.ErrorResponse("Skill assignment not found"));
                }

                return Ok(ApiResponse<object>.SuccessResponse(null, "Skill removed from user successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing skill {SkillId} from user {UserId}", skillId, userId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to remove skill from user"));
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<ApiResponse<IEnumerable<UserSkillDto>>>> GetUserSkills(int userId)
        {
            try
            {
                var skills = await _skillService.GetUserSkillsAsync(userId);
                return Ok(ApiResponse<IEnumerable<UserSkillDto>>.SuccessResponse(skills, "User skills retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving skills for user {UserId}", userId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve user skills"));
            }
        }

        [HttpPost("role/{roleId}/assign/{skillId}")]
        public async Task<ActionResult<ApiResponse<object>>> AssignSkillToRole(int roleId, int skillId, [FromBody] AssignSkillToRoleRequest? request = null)
        {
            try
            {
                var assignedBy = "System"; // TODO: Get from authenticated user
                var success = await _skillService.AssignSkillToRoleAsync(roleId, skillId, assignedBy, request?.Notes);
                
                return Ok(ApiResponse<object>.SuccessResponse(null, "Skill assigned to role successfully"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning skill {SkillId} to role {RoleId}", skillId, roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to assign skill to role"));
            }
        }

        [HttpDelete("role/{roleId}/remove/{skillId}")]
        public async Task<ActionResult<ApiResponse<object>>> RemoveSkillFromRole(int roleId, int skillId)
        {
            try
            {
                var success = await _skillService.RemoveSkillFromRoleAsync(roleId, skillId);
                if (!success)
                {
                    return NotFound(ApiResponse<object>.ErrorResponse("Skill assignment not found"));
                }

                return Ok(ApiResponse<object>.SuccessResponse(null, "Skill removed from role successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing skill {SkillId} from role {RoleId}", skillId, roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to remove skill from role"));
            }
        }

        [HttpGet("role/{roleId}")]
        public async Task<ActionResult<ApiResponse<IEnumerable<SkillDto>>>> GetRoleSkills(int roleId)
        {
            try
            {
                var skills = await _skillService.GetRoleSkillsAsync(roleId);
                return Ok(ApiResponse<IEnumerable<SkillDto>>.SuccessResponse(skills, "Role skills retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving skills for role {RoleId}", roleId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to retrieve role skills"));
            }
        }
    }
}
