using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MyApi.Modules.Projects.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProjectColumnsController : ControllerBase
    {
        private readonly IProjectColumnService _columnService;
        private readonly ILogger<ProjectColumnsController> _logger;

        public ProjectColumnsController(IProjectColumnService columnService, ILogger<ProjectColumnsController> logger)
        {
            _columnService = columnService;
            _logger = logger;
        }

        /// <summary>
        /// Get all columns for a project
        /// </summary>
        [HttpGet("project/{projectId}")]
        public async Task<ActionResult<ProjectColumnListResponseDto>> GetProjectColumns(int projectId)
        {
            try
            {
                var result = await _columnService.GetProjectColumnsAsync(projectId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting columns for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while retrieving project columns");
            }
        }

        /// <summary>
        /// Get column by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectColumnResponseDto>> GetColumn(int id)
        {
            try
            {
                var column = await _columnService.GetColumnByIdAsync(id);
                
                if (column == null)
                {
                    return NotFound($"Column with ID {id} not found");
                }

                return Ok(column);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting column with ID {ColumnId}", id);
                return StatusCode(500, "An error occurred while retrieving the column");
            }
        }

        /// <summary>
        /// Create a new project column
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ProjectColumnResponseDto>> CreateColumn([FromBody] CreateProjectColumnRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var column = await _columnService.CreateColumnAsync(createDto, currentUser);

                return CreatedAtAction(nameof(GetColumn), new { id = column.Id }, column);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project column");
                return StatusCode(500, "An error occurred while creating the column");
            }
        }

        /// <summary>
        /// Update an existing project column
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ProjectColumnResponseDto>> UpdateColumn(int id, [FromBody] UpdateProjectColumnRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var column = await _columnService.UpdateColumnAsync(id, updateDto, currentUser);

                if (column == null)
                {
                    return NotFound($"Column with ID {id} not found");
                }

                return Ok(column);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating column with ID {ColumnId}", id);
                return StatusCode(500, "An error occurred while updating the column");
            }
        }

        /// <summary>
        /// Delete a project column
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteColumn(int id, [FromQuery] int? moveTasksToColumnId = null)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var success = await _columnService.DeleteColumnAsync(id, moveTasksToColumnId, currentUser);

                if (!success)
                {
                    return NotFound($"Column with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting column with ID {ColumnId}", id);
                return StatusCode(500, "An error occurred while deleting the column");
            }
        }

        /// <summary>
        /// Reorder project columns
        /// </summary>
        [HttpPut("project/{projectId}/reorder")]
        public async Task<ActionResult> ReorderColumns(int projectId, [FromBody] ReorderProjectColumnsRequestDto reorderDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var success = await _columnService.ReorderColumnsAsync(projectId, reorderDto, currentUser);

                if (!success)
                {
                    return BadRequest("Failed to reorder columns");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering columns for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while reordering columns");
            }
        }

        private string GetCurrentUser()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst("email")?.Value ?? 
                   "system";
        }
    }
}
