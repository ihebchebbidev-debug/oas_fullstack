using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApi.Modules.Planning.DTOs;
using MyApi.Modules.Planning.Services;
using MyApi.Modules.Shared.Services;

namespace MyApi.Modules.Planning.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/planning")]
    public class PlanningController : ControllerBase
    {
        private readonly IPlanningService _planningService;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<PlanningController> _logger;

        public PlanningController(
            IPlanningService planningService, 
            ISystemLogService systemLogService,
            ILogger<PlanningController> logger)
        {
            _planningService = planningService;
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
        /// Get unassigned jobs ready for scheduling
        /// </summary>
        [HttpGet("unassigned-jobs")]
        public async Task<IActionResult> GetUnassignedJobs(
            [FromQuery] string? priority = null,
            [FromQuery] string? required_skills = null,
            [FromQuery] string? service_order_id = null,
            [FromQuery] int page = 1,
            [FromQuery] int page_size = 20)
        {
            try
            {
                var skills = string.IsNullOrEmpty(required_skills)
                    ? null
                    : required_skills.Split(',').ToList();

                var result = await _planningService.GetUnassignedJobsAsync(
                    priority, skills, service_order_id, page, page_size);

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unassigned jobs");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// Assign a job to one or more users
        /// </summary>
        [HttpPost("assign")]
        public async Task<IActionResult> AssignJob([FromBody] AssignJobDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var result = await _planningService.AssignJobAsync(dto, currentUserId);

                await _systemLogService.LogSuccessAsync($"Job {dto.JobId} assigned to technicians", "Planning", "update", currentUserId, GetCurrentUserName(), "Job", dto.JobId.ToString());

                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                await _systemLogService.LogWarningAsync($"Failed to assign job: {ex.Message}", "Planning", "update", GetCurrentUserId(), GetCurrentUserName(), "Job", dto.JobId.ToString(), ex.Message);
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            catch (InvalidOperationException ex)
            {
                await _systemLogService.LogWarningAsync($"Invalid job assignment: {ex.Message}", "Planning", "update", GetCurrentUserId(), GetCurrentUserName(), "Job", dto.JobId.ToString(), ex.Message);
                return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning job");
                await _systemLogService.LogErrorAsync($"Failed to assign job {dto.JobId}", "Planning", "update", GetCurrentUserId(), GetCurrentUserName(), "Job", dto.JobId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// Assign multiple jobs at once
        /// </summary>
        [HttpPost("batch-assign")]
        public async Task<IActionResult> BatchAssign([FromBody] BatchAssignDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var result = await _planningService.BatchAssignAsync(dto, currentUserId);

                await _systemLogService.LogSuccessAsync($"Batch assignment completed: {dto.Assignments?.Count ?? 0} jobs assigned", "Planning", "update", currentUserId, GetCurrentUserName(), "Job");

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch assigning jobs");
                await _systemLogService.LogErrorAsync("Batch job assignment failed", "Planning", "update", GetCurrentUserId(), GetCurrentUserName(), "Job", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// Validate assignment before committing (checks conflicts, availability, skills)
        /// </summary>
        [HttpPost("validate-assignment")]
        public async Task<IActionResult> ValidateAssignment([FromBody] ValidateAssignmentDto dto)
        {
            try
            {
                var result = await _planningService.ValidateAssignmentAsync(dto);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating assignment");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// Get user's schedule for a date range
        /// </summary>
        [HttpGet("user-schedule/{userId}")]
        public async Task<IActionResult> GetUserSchedule(
            string userId,
            [FromQuery] string start_date,
            [FromQuery] string end_date)
        {
            try
            {
                if (!DateTime.TryParse(start_date, out var startDate) ||
                    !DateTime.TryParse(end_date, out var endDate))
                {
                    return BadRequest(new { success = false, error = new { code = "INVALID_DATES", message = "Invalid date format" } });
                }

                var result = await _planningService.GetUserScheduleAsync(userId, startDate, endDate);
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user schedule");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// Get available users for a specific time slot
        /// </summary>
        [HttpGet("available-users")]
        public async Task<IActionResult> GetAvailableUsers(
            [FromQuery] string date,
            [FromQuery] string start_time,
            [FromQuery] string end_time,
            [FromQuery] string? required_skills = null)
        {
            try
            {
                if (!DateTime.TryParse(date, out var parsedDate) ||
                    !TimeSpan.TryParse(start_time, out var startTimeSpan) ||
                    !TimeSpan.TryParse(end_time, out var endTimeSpan))
                {
                    return BadRequest(new { success = false, error = new { code = "INVALID_PARAMETERS", message = "Invalid date or time format" } });
                }

                var skills = string.IsNullOrEmpty(required_skills)
                    ? null
                    : required_skills.Split(',').ToList();

                var result = await _planningService.GetAvailableUsersAsync(
                    parsedDate, startTimeSpan, endTimeSpan, skills);

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching available users");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // ===================== SCHEDULE MANAGEMENT =====================

        /// <summary>
        /// Get full schedule configuration for a user (working hours + leaves)
        /// </summary>
        [HttpGet("schedule/{userId}")]
        public async Task<IActionResult> GetUserFullSchedule(int userId)
        {
            try
            {
                var result = await _planningService.GetUserFullScheduleAsync(userId);
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user full schedule");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// Update user schedule (working hours, status)
        /// </summary>
        [HttpPut("schedule")]
        public async Task<IActionResult> UpdateUserSchedule([FromBody] UpdateUserScheduleDto dto)
        {
            try
            {
                var result = await _planningService.UpdateUserScheduleAsync(dto);

                await _systemLogService.LogSuccessAsync($"User schedule updated for user {dto.UserId}", "Planning", "update", GetCurrentUserId(), GetCurrentUserName(), "UserSchedule", dto.UserId.ToString());

                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user schedule");
                await _systemLogService.LogErrorAsync($"Failed to update schedule for user {dto.UserId}", "Planning", "update", GetCurrentUserId(), GetCurrentUserName(), "UserSchedule", dto.UserId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // ===================== LEAVE MANAGEMENT =====================

        /// <summary>
        /// Get all leaves for a user
        /// </summary>
        [HttpGet("leaves/{userId}")]
        public async Task<IActionResult> GetUserLeaves(int userId)
        {
            try
            {
                var result = await _planningService.GetUserLeavesAsync(userId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user leaves");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// Create a new leave entry for a user
        /// </summary>
        [HttpPost("leaves")]
        public async Task<IActionResult> CreateLeave([FromBody] CreateLeaveDto dto)
        {
            try
            {
                var result = await _planningService.CreateLeaveAsync(dto);

                await _systemLogService.LogSuccessAsync($"Leave created for user {dto.UserId}: {dto.LeaveType}", "Planning", "create", GetCurrentUserId(), GetCurrentUserName(), "Leave", result.Id.ToString());

                return Ok(new { success = true, data = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = ex.Message, message = ex.Message } });
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error creating leave");
                await _systemLogService.LogErrorAsync($"Failed to create leave for user {dto.UserId}", "Planning", "create", GetCurrentUserId(), GetCurrentUserName(), "Leave", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// Update an existing leave entry
        /// </summary>
        [HttpPut("leaves/{leaveId}")]
        public async Task<IActionResult> UpdateLeave(int leaveId, [FromBody] UpdateLeaveDto dto)
        {
            try
            {
                var result = await _planningService.UpdateLeaveAsync(leaveId, dto);

                await _systemLogService.LogSuccessAsync($"Leave {leaveId} updated", "Planning", "update", GetCurrentUserId(), GetCurrentUserName(), "Leave", leaveId.ToString());

                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            catch (InvalidOperationException ex)
            {
                // Business rule (overlap / allowance / bad range) — must not be a 500.
                return BadRequest(new { success = false, error = new { code = ex.Message, message = ex.Message } });
            }
            catch (Exception ex)

            {
                _logger.LogError(ex, "Error updating leave");
                await _systemLogService.LogErrorAsync($"Failed to update leave {leaveId}", "Planning", "update", GetCurrentUserId(), GetCurrentUserName(), "Leave", leaveId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// Delete a leave entry
        /// </summary>
        [HttpDelete("leaves/{leaveId}")]
        public async Task<IActionResult> DeleteLeave(int leaveId)
        {
            try
            {
                await _planningService.DeleteLeaveAsync(leaveId);

                await _systemLogService.LogSuccessAsync($"Leave {leaveId} deleted", "Planning", "delete", GetCurrentUserId(), GetCurrentUserName(), "Leave", leaveId.ToString());

                return Ok(new { success = true, message = "Leave deleted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting leave");
                await _systemLogService.LogErrorAsync($"Failed to delete leave {leaveId}", "Planning", "delete", GetCurrentUserId(), GetCurrentUserName(), "Leave", leaveId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }
    }
}
