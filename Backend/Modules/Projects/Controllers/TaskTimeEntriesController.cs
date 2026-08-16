using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Services;
using System.Security.Claims;

namespace MyApi.Modules.Projects.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TaskTimeEntriesController : ControllerBase
    {
        private readonly ITaskTimeEntryService _timeEntryService;

        public TaskTimeEntriesController(ITaskTimeEntryService timeEntryService)
        {
            _timeEntryService = timeEntryService;
        }

        private string GetCurrentUser()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   "system";
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        // CRUD Operations
        [HttpPost]
        public async Task<ActionResult<TaskTimeEntryResponseDto>> CreateTimeEntry([FromBody] CreateTaskTimeEntryDto createDto)
        {
            if (createDto.ProjectTaskId == null && createDto.DailyTaskId == null)
            {
                return BadRequest("Either ProjectTaskId or DailyTaskId must be provided");
            }

            // Set current user if not provided
            if (!createDto.UserId.HasValue)
            {
                createDto.UserId = GetCurrentUserId();
            }

            var result = await _timeEntryService.CreateTimeEntryAsync(createDto, GetCurrentUser());
            return CreatedAtAction(nameof(GetTimeEntry), new { id = result.Id }, result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TaskTimeEntryResponseDto>> GetTimeEntry(int id)
        {
            var result = await _timeEntryService.GetTimeEntryByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<TaskTimeEntryResponseDto>> UpdateTimeEntry(int id, [FromBody] UpdateTaskTimeEntryDto updateDto)
        {
            var result = await _timeEntryService.UpdateTimeEntryAsync(id, updateDto, GetCurrentUser());
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTimeEntry(int id)
        {
            var result = await _timeEntryService.DeleteTimeEntryAsync(id, GetCurrentUser());
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        // Query Operations
        [HttpGet("project-task/{projectTaskId}")]
        public async Task<ActionResult<List<TaskTimeEntryResponseDto>>> GetTimeEntriesForProjectTask(int projectTaskId)
        {
            var result = await _timeEntryService.GetTimeEntriesForProjectTaskAsync(projectTaskId);
            return Ok(result);
        }

        [HttpGet("daily-task/{dailyTaskId}")]
        public async Task<ActionResult<List<TaskTimeEntryResponseDto>>> GetTimeEntriesForDailyTask(int dailyTaskId)
        {
            var result = await _timeEntryService.GetTimeEntriesForDailyTaskAsync(dailyTaskId);
            return Ok(result);
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<List<TaskTimeEntryResponseDto>>> GetTimeEntriesByUser(
            int userId, 
            [FromQuery] DateTime? fromDate = null, 
            [FromQuery] DateTime? toDate = null)
        {
            var result = await _timeEntryService.GetTimeEntriesByUserAsync(userId, fromDate, toDate);
            return Ok(result);
        }

        [HttpGet("project/{projectId}")]
        public async Task<ActionResult<List<TaskTimeEntryResponseDto>>> GetTimeEntriesByProject(
            int projectId, 
            [FromQuery] DateTime? fromDate = null, 
            [FromQuery] DateTime? toDate = null)
        {
            var result = await _timeEntryService.GetTimeEntriesByProjectAsync(projectId, fromDate, toDate);
            return Ok(result);
        }

        [HttpPost("query")]
        public async Task<ActionResult<List<TaskTimeEntryResponseDto>>> QueryTimeEntries([FromBody] TaskTimeEntryQueryDto query)
        {
            var result = await _timeEntryService.QueryTimeEntriesAsync(query);
            return Ok(result);
        }

        // Summary Operations
        [HttpGet("summary/project-task/{projectTaskId}")]
        public async Task<ActionResult<TaskTimeTrackingSummaryDto>> GetProjectTaskTimeSummary(int projectTaskId)
        {
            var result = await _timeEntryService.GetProjectTaskTimeSummaryAsync(projectTaskId);
            return Ok(result);
        }

        [HttpGet("summary/daily-task/{dailyTaskId}")]
        public async Task<ActionResult<TaskTimeTrackingSummaryDto>> GetDailyTaskTimeSummary(int dailyTaskId)
        {
            var result = await _timeEntryService.GetDailyTaskTimeSummaryAsync(dailyTaskId);
            return Ok(result);
        }

        [HttpGet("total-time")]
        public async Task<ActionResult<decimal>> GetTotalLoggedTime(
            [FromQuery] int? projectTaskId = null, 
            [FromQuery] int? dailyTaskId = null)
        {
            var result = await _timeEntryService.GetTotalLoggedTimeForTaskAsync(projectTaskId, dailyTaskId);
            return Ok(result);
        }

        // Approval Operations
        [HttpPost("{id}/approve")]
        public async Task<ActionResult<TaskTimeEntryResponseDto>> ApproveTimeEntry(
            int id, 
            [FromBody] ApproveTaskTimeEntryDto approveDto)
        {
            var approverId = GetCurrentUserId();
            var result = await _timeEntryService.ApproveTimeEntryAsync(id, approveDto, approverId);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        [HttpPost("bulk-approve")]
        public async Task<ActionResult> BulkApproveTimeEntries([FromBody] BulkApproveTimeEntriesDto bulkDto)
        {
            var approverId = GetCurrentUserId();
            var result = await _timeEntryService.BulkApproveTimeEntriesAsync(bulkDto, approverId);
            if (!result)
            {
                return BadRequest("Failed to process bulk approval");
            }
            return Ok(new { message = "Bulk approval processed successfully" });
        }

        // Timer Operations
        [HttpPost("timer/start")]
        public async Task<ActionResult<TaskTimeEntryResponseDto>> StartTimer(
            [FromQuery] int? projectTaskId = null, 
            [FromQuery] int? dailyTaskId = null,
            [FromQuery] string? workType = "work")
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var result = await _timeEntryService.StartTimerAsync(projectTaskId, dailyTaskId, userId, workType);
            return Ok(result);
        }

        [HttpPost("timer/{id}/stop")]
        public async Task<ActionResult<TaskTimeEntryResponseDto>> StopTimer(
            int id, 
            [FromQuery] string? description = null)
        {
            var result = await _timeEntryService.StopTimerAsync(id, description);
            if (result == null)
            {
                return NotFound("Timer not found or already stopped");
            }
            return Ok(result);
        }

        [HttpGet("timer/active")]
        public async Task<ActionResult<TaskTimeEntryResponseDto>> GetActiveTimer()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var result = await _timeEntryService.GetActiveTimerAsync(userId);
            if (result == null)
            {
                return NotFound("No active timer found");
            }
            return Ok(result);
        }

        // My Time Entries (current user)
        [HttpGet("my-entries")]
        public async Task<ActionResult<List<TaskTimeEntryResponseDto>>> GetMyTimeEntries(
            [FromQuery] DateTime? fromDate = null, 
            [FromQuery] DateTime? toDate = null)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var result = await _timeEntryService.GetTimeEntriesByUserAsync(userId, fromDate, toDate);
            return Ok(result);
        }
    }
}
