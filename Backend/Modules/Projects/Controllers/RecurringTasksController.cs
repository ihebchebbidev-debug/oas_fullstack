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
    public class RecurringTasksController : ControllerBase
    {
        private readonly IRecurringTaskService _recurringTaskService;

        public RecurringTasksController(IRecurringTaskService recurringTaskService)
        {
            _recurringTaskService = recurringTaskService;
        }

        private string GetCurrentUser()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   "system";
        }

        // CRUD
        [HttpPost]
        public async Task<ActionResult<RecurringTaskResponseDto>> Create([FromBody] CreateRecurringTaskDto dto)
        {
            if (dto.ProjectTaskId == null && dto.DailyTaskId == null)
            {
                return BadRequest("Either ProjectTaskId or DailyTaskId must be provided");
            }

            var result = await _recurringTaskService.CreateRecurringTaskAsync(dto, GetCurrentUser());
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RecurringTaskResponseDto>> GetById(int id)
        {
            var result = await _recurringTaskService.GetRecurringTaskByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RecurringTaskResponseDto>> Update(int id, [FromBody] UpdateRecurringTaskDto dto)
        {
            var result = await _recurringTaskService.UpdateRecurringTaskAsync(id, dto, GetCurrentUser());
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var success = await _recurringTaskService.DeleteRecurringTaskAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        // Query by task
        [HttpGet("project-task/{projectTaskId}")]
        public async Task<ActionResult<List<RecurringTaskResponseDto>>> GetForProjectTask(int projectTaskId)
        {
            var result = await _recurringTaskService.GetRecurringTasksForProjectTaskAsync(projectTaskId);
            return Ok(result);
        }

        [HttpGet("daily-task/{dailyTaskId}")]
        public async Task<ActionResult<List<RecurringTaskResponseDto>>> GetForDailyTask(int dailyTaskId)
        {
            var result = await _recurringTaskService.GetRecurringTasksForDailyTaskAsync(dailyTaskId);
            return Ok(result);
        }

        [HttpGet("active")]
        public async Task<ActionResult<List<RecurringTaskResponseDto>>> GetAllActive()
        {
            var result = await _recurringTaskService.GetAllActiveRecurringTasksAsync();
            return Ok(result);
        }

        [HttpGet("{id}/logs")]
        public async Task<ActionResult<List<RecurringTaskLogResponseDto>>> GetLogs(int id, [FromQuery] int limit = 50)
        {
            var result = await _recurringTaskService.GetLogsForRecurringTaskAsync(id, limit);
            return Ok(result);
        }

        // Actions
        [HttpPost("{id}/pause")]
        public async Task<ActionResult<RecurringTaskResponseDto>> Pause(int id)
        {
            var result = await _recurringTaskService.PauseRecurringTaskAsync(id, GetCurrentUser());
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("{id}/resume")]
        public async Task<ActionResult<RecurringTaskResponseDto>> Resume(int id)
        {
            var result = await _recurringTaskService.ResumeRecurringTaskAsync(id, GetCurrentUser());
            if (result == null) return NotFound();
            return Ok(result);
        }

        // Generation (typically called by a scheduled job)
        [HttpPost("generate")]
        public async Task<ActionResult<GenerateResultDto>> GenerateDueTasks([FromBody] GenerateRecurringTasksDto dto)
        {
            var result = await _recurringTaskService.GenerateDueTasksAsync(dto.UpToDate, dto.DryRun);
            return Ok(result);
        }

        [HttpGet("{id}/next-occurrence")]
        public async Task<ActionResult<object>> GetNextOccurrence(int id)
        {
            var next = await _recurringTaskService.CalculateNextOccurrenceAsync(id);
            return Ok(new { nextOccurrence = next });
        }
    }
}
