using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Installations.DTOs;
using MyApi.Modules.Installations.Services;
using MyApi.Modules.Shared.Services;
using System.Security.Claims;

namespace MyApi.Modules.Installations.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InstallationsController : ControllerBase
    {
        private readonly IInstallationService _installationService;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<InstallationsController> _logger;

        public InstallationsController(
            IInstallationService installationService, 
            ISystemLogService systemLogService,
            ILogger<InstallationsController> logger)
        {
            _installationService = installationService;
            _systemLogService = systemLogService;
            _logger = logger;
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        }

        private string GetCurrentUserName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst("FirstName")?.Value + " " + User.FindFirst("LastName")?.Value ?? 
                   User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   "anonymous";
        }

        /// <summary>
        /// Get all installations with optional filters
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetInstallations(
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? contactId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string sortBy = "created_at",
            [FromQuery] string sortOrder = "desc"
        )
        {
            try
            {
                var result = await _installationService.GetInstallationsAsync(
                    search, status, contactId, page, pageSize, sortBy, sortOrder
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching installations");
                await _systemLogService.LogErrorAsync("Failed to retrieve installations", "Installations", "read", GetCurrentUserId(), GetCurrentUserName(), details: ex.Message);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching installations" });
            }
        }

        /// <summary>
        /// Search installations
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchInstallations(
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50
        )
        {
            try
            {
                var result = await _installationService.GetInstallationsAsync(
                    searchTerm, status, null, page, pageSize, "created_at", "desc"
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching installations");
                return StatusCode(500, new { success = false, message = "An error occurred while searching installations" });
            }
        }

        /// <summary>
        /// Get single installation by ID
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetInstallationById(int id)
        {
            try
            {
                var installation = await _installationService.GetInstallationByIdAsync(id);
                if (installation == null)
                {
                    return NotFound(new { success = false, message = "Installation not found" });
                }

                return Ok(installation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching installation {InstallationId}", id);
                await _systemLogService.LogErrorAsync($"Failed to retrieve installation {id}", "Installations", "read", GetCurrentUserId(), GetCurrentUserName(), "Installation", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching the installation" });
            }
        }

        /// <summary>
        /// Create a new installation
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateInstallation([FromBody] CreateInstallationDto createDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var installation = await _installationService.CreateInstallationAsync(createDto, userId);

                await _systemLogService.LogSuccessAsync($"Installation created: {createDto.Name}", "Installations", "create", userId, GetCurrentUserName(), "Installation", installation.Id.ToString());

                return CreatedAtAction(nameof(GetInstallationById), new { id = installation.Id }, installation);
            }
            catch (KeyNotFoundException ex)
            {
                await _systemLogService.LogWarningAsync($"Failed to create installation: {ex.Message}", "Installations", "create", GetCurrentUserId(), GetCurrentUserName(), "Installation", details: ex.Message);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating installation");
                await _systemLogService.LogErrorAsync("Failed to create installation", "Installations", "create", GetCurrentUserId(), GetCurrentUserName(), "Installation", details: ex.Message);
                return StatusCode(500, new { success = false, message = "An error occurred while creating the installation" });
            }
        }

        /// <summary>
        /// Update an installation
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateInstallation(int id, [FromBody] UpdateInstallationDto updateDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var installation = await _installationService.UpdateInstallationAsync(id, updateDto, userId);

                if (installation == null)
                {
                    return NotFound(new { success = false, message = "Installation not found" });
                }

                await _systemLogService.LogSuccessAsync($"Installation updated: {installation.Name}", "Installations", "update", userId, GetCurrentUserName(), "Installation", id.ToString());

                return Ok(installation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating installation {InstallationId}", id);
                await _systemLogService.LogErrorAsync($"Failed to update installation {id}", "Installations", "update", GetCurrentUserId(), GetCurrentUserName(), "Installation", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, message = "An error occurred while updating the installation" });
            }
        }

        /// <summary>
        /// Delete an installation
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteInstallation(int id)
        {
            try
            {
                var result = await _installationService.DeleteInstallationAsync(id, GetCurrentUserId());
                if (!result)
                {
                    return NotFound(new { success = false, message = "Installation not found" });
                }

                await _systemLogService.LogSuccessAsync($"Installation deleted: ID {id}", "Installations", "delete", GetCurrentUserId(), GetCurrentUserName(), "Installation", id.ToString());

                return Ok(new { success = true, message = "Installation deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting installation {InstallationId}", id);
                await _systemLogService.LogErrorAsync($"Failed to delete installation {id}", "Installations", "delete", GetCurrentUserId(), GetCurrentUserName(), "Installation", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, message = "An error occurred while deleting the installation" });
            }
        }

        /// <summary>
        /// Get maintenance history for an installation
        /// </summary>
        [HttpGet("{id:int}/maintenance-history")]
        public async Task<IActionResult> GetMaintenanceHistory(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var histories = await _installationService.GetMaintenanceHistoryAsync(id, page, pageSize);
                return Ok(new { success = true, data = histories });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching maintenance history for installation {InstallationId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching maintenance history" });
            }
        }

        /// <summary>
        /// Add maintenance history record
        /// </summary>
        [HttpPost("{id:int}/maintenance-history")]
        public async Task<IActionResult> AddMaintenanceHistory(int id, [FromBody] CreateMaintenanceHistoryDto historyDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var history = await _installationService.AddMaintenanceHistoryAsync(id, historyDto, userId);

                await _systemLogService.LogSuccessAsync($"Maintenance history added for installation {id}: {historyDto.MaintenanceType}", "Installations", "create", userId, GetCurrentUserName(), "MaintenanceHistory", history.Id.ToString());

                return CreatedAtAction(nameof(GetInstallationById), new { id }, history);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, message = "Installation not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding maintenance history for installation {InstallationId}", id);
                await _systemLogService.LogErrorAsync($"Failed to add maintenance history for installation {id}", "Installations", "create", GetCurrentUserId(), GetCurrentUserName(), "MaintenanceHistory", details: ex.Message);
                return StatusCode(500, new { success = false, message = "An error occurred while adding maintenance history" });
            }
        }

        // =====================================================
        // Bulk Import Endpoint - Supports up to 10,000+ records
        // =====================================================

        /// <summary>
        /// Bulk import installations with batch processing for high performance.
        /// Supports up to 10,000+ records with automatic batching.
        /// </summary>
        [HttpPost("import")]
        public async Task<IActionResult> BulkImportInstallations([FromBody] BulkImportInstallationRequestDto importRequest)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userName = GetCurrentUserName();

                _logger.LogInformation("Starting bulk import of {Count} installations by user {UserId}", 
                    importRequest.Installations.Count, userId);

                var result = await _installationService.BulkImportInstallationsAsync(importRequest, userId);

                await _systemLogService.LogSuccessAsync(
                    $"Bulk imported {result.SuccessCount} installations ({result.FailedCount} failures, {result.SkippedCount} skipped)", 
                    "Installations", "import", userId, userName, "Installation");

                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk import of installations");
                await _systemLogService.LogErrorAsync("Bulk import failed", "Installations", "import", GetCurrentUserId(), GetCurrentUserName(), "Installation", details: ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred during bulk import",
                    error = ex.Message
                });
            }
        }
    }
}
