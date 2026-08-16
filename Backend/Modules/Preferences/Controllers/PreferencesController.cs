using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Preferences.DTOs;
using MyApi.Modules.Preferences.Services;
using System.Security.Claims;

namespace MyApi.Modules.Preferences.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PreferencesController : ControllerBase
    {
        private readonly IPreferenceService _preferenceService;

        public PreferencesController(IPreferenceService preferenceService)
        {
            _preferenceService = preferenceService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                throw new UnauthorizedAccessException("User ID not found in token");
            }
            return userId;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyPreferences()
        {
            try
            {
                var userId = GetCurrentUserId();
                var preferences = await _preferenceService.GetByUserIdAsync(userId);
                
                if (preferences == null)
                {
                    return Ok(new { success = true, message = "No preferences found", data = (object?)null });
                }

                return Ok(new { success = true, message = "Preferences retrieved successfully", data = preferences });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { success = false, message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserPreferences(int userId)
        {
            try
            {
                var preferences = await _preferenceService.GetByUserIdAsync(userId);
                
                if (preferences == null)
                {
                    return Ok(new { success = true, message = "No preferences found", data = (object?)null });
                }

                return Ok(new { success = true, message = "Preferences retrieved successfully", data = preferences });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateMyPreferences([FromBody] CreatePreferenceDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var preferences = await _preferenceService.CreateAsync(userId, dto);
                return Ok(new { success = true, message = "Preferences created successfully", data = preferences });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { success = false, message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{userId}")]
        public async Task<IActionResult> CreateUserPreferences(int userId, [FromBody] CreatePreferenceDto dto)
        {
            try
            {
                var preferences = await _preferenceService.CreateAsync(userId, dto);
                return Ok(new { success = true, message = "Preferences created successfully", data = preferences });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateMyPreferences([FromBody] UpdatePreferenceDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var preferences = await _preferenceService.UpdateAsync(userId, dto);
                
                if (preferences == null)
                {
                    return NotFound(new { success = false, message = "Preferences not found" });
                }

                return Ok(new { success = true, message = "Preferences updated successfully", data = preferences });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { success = false, message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateUserPreferences(int userId, [FromBody] UpdatePreferenceDto dto)
        {
            try
            {
                var preferences = await _preferenceService.UpdateAsync(userId, dto);
                
                if (preferences == null)
                {
                    return NotFound(new { success = false, message = "Preferences not found" });
                }

                return Ok(new { success = true, message = "Preferences updated successfully", data = preferences });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteMyPreferences()
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _preferenceService.DeleteAsync(userId);
                
                if (!result)
                {
                    return NotFound(new { success = false, message = "Preferences not found" });
                }

                return Ok(new { success = true, message = "Preferences deleted successfully" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { success = false, message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}