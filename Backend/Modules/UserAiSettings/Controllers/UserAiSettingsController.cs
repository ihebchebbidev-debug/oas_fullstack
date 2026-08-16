using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.UserAiSettings.DTOs;
using MyApi.Modules.UserAiSettings.Services;
using System.Security.Claims;

namespace MyApi.Modules.UserAiSettings.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserAiSettingsController : ControllerBase
    {
        private readonly IUserAiSettingsService _service;
        private readonly ILogger<UserAiSettingsController> _logger;

        public UserAiSettingsController(IUserAiSettingsService service, ILogger<UserAiSettingsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Extracts UserId from JWT claims. Works for both MainAdminUser and RegularUser.
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                throw new UnauthorizedAccessException("User ID not found in token");
            }
            return userId;
        }

        /// <summary>
        /// Extracts UserType from JWT claims. Returns "MainAdminUser" or "RegularUser".
        /// </summary>
        private string GetCurrentUserType()
        {
            return User.FindFirst("UserType")?.Value ?? "MainAdminUser";
        }

        // ═══════════════════════════════════════════
        //  KEYS
        // ═══════════════════════════════════════════

        /// <summary>
        /// Get all AI API keys for the authenticated user (masked).
        /// GET /api/UserAiSettings/keys
        /// </summary>
        [HttpGet("keys")]
        public async Task<IActionResult> GetKeys()
        {
            try
            {
                var userId = GetCurrentUserId();
                var userType = GetCurrentUserType();
                var keys = await _service.GetKeysAsync(userId, userType);
                return Ok(keys);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AI keys");
                return StatusCode(500, new { message = "Failed to retrieve AI keys" });
            }
        }

        /// <summary>
        /// Add a new AI API key.
        /// POST /api/UserAiSettings/keys
        /// </summary>
        [HttpPost("keys")]
        public async Task<IActionResult> AddKey([FromBody] CreateUserAiKeyDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.ApiKey))
                    return BadRequest(new { message = "API key is required" });

                var userId = GetCurrentUserId();
                var userType = GetCurrentUserType();
                var key = await _service.AddKeyAsync(userId, userType, dto);
                return Ok(key);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding AI key");
                return StatusCode(500, new { message = "Failed to add AI key" });
            }
        }

        /// <summary>
        /// Update an existing AI API key.
        /// PUT /api/UserAiSettings/keys/{id}
        /// </summary>
        [HttpPut("keys/{id}")]
        public async Task<IActionResult> UpdateKey(int id, [FromBody] UpdateUserAiKeyDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userType = GetCurrentUserType();
                var key = await _service.UpdateKeyAsync(userId, userType, id, dto);

                if (key == null)
                    return NotFound(new { message = $"Key {id} not found" });

                return Ok(key);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating AI key {KeyId}", id);
                return StatusCode(500, new { message = "Failed to update AI key" });
            }
        }

        /// <summary>
        /// Delete an AI API key.
        /// DELETE /api/UserAiSettings/keys/{id}
        /// </summary>
        [HttpDelete("keys/{id}")]
        public async Task<IActionResult> DeleteKey(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userType = GetCurrentUserType();
                var success = await _service.DeleteKeyAsync(userId, userType, id);

                if (!success)
                    return NotFound(new { message = $"Key {id} not found" });

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting AI key {KeyId}", id);
                return StatusCode(500, new { message = "Failed to delete AI key" });
            }
        }

        /// <summary>
        /// Reorder AI API keys (set priority based on array position).
        /// POST /api/UserAiSettings/keys/reorder
        /// </summary>
        [HttpPost("keys/reorder")]
        public async Task<IActionResult> ReorderKeys([FromBody] ReorderKeysDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userType = GetCurrentUserType();
                var success = await _service.ReorderKeysAsync(userId, userType, dto.KeyIds);

                if (!success)
                    return BadRequest(new { message = "Failed to reorder keys" });

                return Ok(new { message = "Keys reordered successfully" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering AI keys");
                return StatusCode(500, new { message = "Failed to reorder AI keys" });
            }
        }

        // ═══════════════════════════════════════════
        //  PREFERENCES
        // ═══════════════════════════════════════════

        /// <summary>
        /// Get AI preferences for the authenticated user.
        /// GET /api/UserAiSettings/preferences
        /// </summary>
        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences()
        {
            try
            {
                var userId = GetCurrentUserId();
                var userType = GetCurrentUserType();
                var prefs = await _service.GetPreferencesAsync(userId, userType);
                return Ok(prefs);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AI preferences");
                return StatusCode(500, new { message = "Failed to retrieve AI preferences" });
            }
        }

        /// <summary>
        /// Create or update AI preferences for the authenticated user.
        /// PUT /api/UserAiSettings/preferences
        /// </summary>
        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdateUserAiPreferencesDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userType = GetCurrentUserType();
                var prefs = await _service.UpdatePreferencesAsync(userId, userType, dto);
                return Ok(prefs);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating AI preferences");
                return StatusCode(500, new { message = "Failed to update AI preferences" });
            }
        }
    }
}
