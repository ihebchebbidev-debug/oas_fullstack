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
    public class TaskChecklistsController : ControllerBase
    {
        private readonly ITaskChecklistService _checklistService;

        public TaskChecklistsController(ITaskChecklistService checklistService)
        {
            _checklistService = checklistService;
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

        // Checklist CRUD
        [HttpPost]
        public async Task<ActionResult<TaskChecklistResponseDto>> CreateChecklist([FromBody] CreateTaskChecklistDto dto)
        {
            if (dto.ProjectTaskId == null && dto.DailyTaskId == null)
            {
                return BadRequest("Either ProjectTaskId or DailyTaskId must be provided");
            }

            var result = await _checklistService.CreateChecklistAsync(dto, GetCurrentUser());
            return CreatedAtAction(nameof(GetChecklist), new { id = result.Id }, result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TaskChecklistResponseDto>> GetChecklist(int id)
        {
            var result = await _checklistService.GetChecklistByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<TaskChecklistResponseDto>> UpdateChecklist(int id, [FromBody] UpdateTaskChecklistDto dto)
        {
            var result = await _checklistService.UpdateChecklistAsync(id, dto, GetCurrentUser());
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteChecklist(int id)
        {
            var success = await _checklistService.DeleteChecklistAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        // Query by task
        [HttpGet("project-task/{projectTaskId}")]
        public async Task<ActionResult<List<TaskChecklistResponseDto>>> GetChecklistsForProjectTask(int projectTaskId)
        {
            var result = await _checklistService.GetChecklistsForProjectTaskAsync(projectTaskId);
            return Ok(result);
        }

        [HttpGet("daily-task/{dailyTaskId}")]
        public async Task<ActionResult<List<TaskChecklistResponseDto>>> GetChecklistsForDailyTask(int dailyTaskId)
        {
            var result = await _checklistService.GetChecklistsForDailyTaskAsync(dailyTaskId);
            return Ok(result);
        }

        // Checklist Items
        [HttpPost("items")]
        public async Task<ActionResult<TaskChecklistItemResponseDto>> CreateChecklistItem([FromBody] CreateChecklistItemDto dto)
        {
            var result = await _checklistService.CreateChecklistItemAsync(dto, GetCurrentUser());
            return Ok(result);
        }

        [HttpPut("items/{id}")]
        public async Task<ActionResult<TaskChecklistItemResponseDto>> UpdateChecklistItem(int id, [FromBody] UpdateChecklistItemDto dto)
        {
            var result = await _checklistService.UpdateChecklistItemAsync(id, dto, GetCurrentUser());
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("items/{id}")]
        public async Task<ActionResult> DeleteChecklistItem(int id)
        {
            var success = await _checklistService.DeleteChecklistItemAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpPost("items/{id}/toggle")]
        public async Task<ActionResult<TaskChecklistItemResponseDto>> ToggleChecklistItem(int id)
        {
            var userId = GetCurrentUserId();
            var userName = GetCurrentUser();
            var result = await _checklistService.ToggleChecklistItemAsync(id, userId, userName);
            if (result == null) return NotFound();
            return Ok(result);
        }

        // Bulk operations
        [HttpPost("items/bulk")]
        public async Task<ActionResult<List<TaskChecklistItemResponseDto>>> BulkCreateChecklistItems([FromBody] BulkCreateChecklistItemsDto dto)
        {
            var result = await _checklistService.BulkCreateChecklistItemsAsync(dto, GetCurrentUser());
            return Ok(result);
        }

        [HttpPost("items/reorder")]
        public async Task<ActionResult> ReorderChecklistItems([FromBody] ReorderChecklistItemsDto dto)
        {
            await _checklistService.ReorderChecklistItemsAsync(dto);
            return Ok();
        }

        // Convert to task
        [HttpPost("items/{id}/convert-to-task")]
        public async Task<ActionResult<object>> ConvertItemToTask(int id)
        {
            try
            {
                var taskId = await _checklistService.ConvertItemToTaskAsync(id, GetCurrentUser());
                return Ok(new { taskId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
