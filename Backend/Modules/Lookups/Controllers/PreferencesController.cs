using MyApi.Modules.Lookups.DTOs;
using MyApi.Modules.Lookups.Services;
using MyApi.Modules.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MyApi.Modules.Lookups.Controllers
{
[ApiController]
    [Route("api/[controller]")]
    public class PreferencesController : ControllerBase
    {
        private readonly IPreferencesService _preferencesService;
        private readonly ILogger<PreferencesController> _logger;

        public PreferencesController(IPreferencesService preferencesService, ILogger<PreferencesController> logger)
        {
            _preferencesService = preferencesService;
            _logger = logger;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserPreferences(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest("User ID is required");
                }

                var preferences = await _preferencesService.GetUserPreferencesAsync(userId);
                
                if (preferences == null)
                {
                    return NotFound("User preferences not found");
                }

                return Ok(new ApiResponse<PreferencesResponse>
                {
                    Success = true,
                    Message = "User preferences retrieved successfully",
                    Data = preferences
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user preferences");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost("{userId}")]
        public async Task<IActionResult> CreateUserPreferences(string userId, [FromBody] CreatePreferencesRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest("User ID is required");
                }

                var preferences = await _preferencesService.CreateUserPreferencesAsync(userId, request);

                return Ok(new ApiResponse<PreferencesResponse>
                {
                    Success = true,
                    Message = "User preferences created successfully",
                    Data = preferences
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user preferences");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateUserPreferences(string userId, [FromBody] UpdatePreferencesRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest("User ID is required");
                }

                var preferences = await _preferencesService.UpdateUserPreferencesAsync(userId, request);
                
                if (preferences == null)
                {
                    return NotFound("User preferences not found");
                }

                return Ok(new ApiResponse<PreferencesResponse>
                {
                    Success = true,
                    Message = "User preferences updated successfully",
                    Data = preferences
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user preferences");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUserPreferences(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest("User ID is required");
                }

                var success = await _preferencesService.DeleteUserPreferencesAsync(userId);
                
                if (!success)
                {
                    return NotFound("User preferences not found");
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "User preferences deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user preferences");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }
    }
}
