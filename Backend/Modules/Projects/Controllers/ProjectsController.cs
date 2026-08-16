using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Services;
using MyApi.Modules.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MyApi.Modules.Projects.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(
            IProjectService projectService, 
            ISystemLogService systemLogService,
            ILogger<ProjectsController> logger)
        {
            _projectService = projectService;
            _systemLogService = systemLogService;
            _logger = logger;
        }

        /// <summary>
        /// Get all projects with optional filtering and pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ProjectListResponseDto>> GetAllProjects([FromQuery] ProjectSearchRequestDto? searchRequest = null)
        {
            try
            {
                var result = await _projectService.GetAllProjectsAsync(searchRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all projects");
                await _systemLogService.LogErrorAsync("Failed to retrieve projects", "Projects", "read", GetCurrentUserId(), GetCurrentUser(), details: ex.Message);
                return StatusCode(500, "An error occurred while retrieving projects");
            }
        }

        /// <summary>
        /// Get project by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectResponseDto>> GetProject(int id)
        {
            try
            {
                var project = await _projectService.GetProjectByIdAsync(id);
                
                if (project == null)
                {
                    return NotFound($"Project with ID {id} not found");
                }

                return Ok(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project with ID {ProjectId}", id);
                await _systemLogService.LogErrorAsync($"Failed to retrieve project {id}", "Projects", "read", GetCurrentUserId(), GetCurrentUser(), "Project", id.ToString(), ex.Message);
                return StatusCode(500, "An error occurred while retrieving the project");
            }
        }

        /// <summary>
        /// Create a new project
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ProjectResponseDto>> CreateProject([FromBody] CreateProjectRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var project = await _projectService.CreateProjectAsync(createDto, currentUser);

                await _systemLogService.LogSuccessAsync($"Project created: {createDto.Name}", "Projects", "create", GetCurrentUserId(), currentUser, "Project", project.Id.ToString());

                return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while creating project");
                await _systemLogService.LogWarningAsync($"Failed to create project: {ex.Message}", "Projects", "create", GetCurrentUserId(), GetCurrentUser(), "Project", details: ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project");
                await _systemLogService.LogErrorAsync("Failed to create project", "Projects", "create", GetCurrentUserId(), GetCurrentUser(), "Project", details: ex.Message);
                return StatusCode(500, "An error occurred while creating the project");
            }
        }

        /// <summary>
        /// Update an existing project
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ProjectResponseDto>> UpdateProject(int id, [FromBody] UpdateProjectRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var project = await _projectService.UpdateProjectAsync(id, updateDto, currentUser);

                if (project == null)
                {
                    return NotFound($"Project with ID {id} not found");
                }

                await _systemLogService.LogSuccessAsync($"Project updated: {project.Name}", "Projects", "update", GetCurrentUserId(), currentUser, "Project", id.ToString());

                return Ok(project);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while updating project with ID {ProjectId}", id);
                await _systemLogService.LogWarningAsync($"Failed to update project {id}: {ex.Message}", "Projects", "update", GetCurrentUserId(), GetCurrentUser(), "Project", id.ToString(), ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project with ID {ProjectId}", id);
                await _systemLogService.LogErrorAsync($"Failed to update project {id}", "Projects", "update", GetCurrentUserId(), GetCurrentUser(), "Project", id.ToString(), ex.Message);
                return StatusCode(500, "An error occurred while updating the project");
            }
        }

        /// <summary>
        /// Delete a project
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteProject(int id)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var success = await _projectService.DeleteProjectAsync(id, currentUser);

                if (!success)
                {
                    return NotFound($"Project with ID {id} not found");
                }

                await _systemLogService.LogSuccessAsync($"Project deleted: ID {id}", "Projects", "delete", GetCurrentUserId(), currentUser, "Project", id.ToString());

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project with ID {ProjectId}", id);
                await _systemLogService.LogErrorAsync($"Failed to delete project {id}", "Projects", "delete", GetCurrentUserId(), GetCurrentUser(), "Project", id.ToString(), ex.Message);
                return StatusCode(500, "An error occurred while deleting the project");
            }
        }

        /// <summary>
        /// Search projects
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<List<ProjectResponseDto>>> SearchProjects([FromQuery] string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return Ok(new { projects = new List<ProjectResponseDto>(), totalCount = 0 });

                var result = await _projectService.SearchProjectsAsync(searchTerm);
                return Ok(new { projects = result, totalCount = result.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching projects with term {SearchTerm}", searchTerm);
                return StatusCode(500, "An error occurred while searching projects");
            }
        }


        /// <summary>
        /// Get project statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<ProjectStatisticsDto>> GetStatistics()
        {
            try
            {
                var stats = await _projectService.GetStatisticsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project statistics");
                return StatusCode(500, "An error occurred while retrieving project statistics");
            }
        }

        /// <summary>
        /// Bulk update the status of several projects
        /// </summary>
        [HttpPost("bulk/status")]
        public async Task<ActionResult> BulkUpdateStatus([FromBody] BulkUpdateProjectStatusRequestDto dto)
        {
            try
            {
                if (dto?.ProjectIds == null || dto.ProjectIds.Count == 0)
                    return BadRequest("projectIds is required");

                var updated = await _projectService.BulkUpdateStatusAsync(dto.ProjectIds, dto.Status, GetCurrentUser());
                return Ok(new { updated });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating project status");
                return StatusCode(500, "An error occurred while updating projects");
            }
        }

        /// <summary>
        /// Bulk archive / un-archive projects
        /// </summary>
        [HttpPost("bulk/archive")]
        public async Task<ActionResult> BulkArchive([FromBody] BulkArchiveProjectsRequestDto dto)
        {
            try
            {
                if (dto?.ProjectIds == null || dto.ProjectIds.Count == 0)
                    return BadRequest("projectIds is required");

                var updated = await _projectService.BulkArchiveAsync(dto.ProjectIds, dto.Archive, GetCurrentUser());
                return Ok(new { updated });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk archiving projects");
                return StatusCode(500, "An error occurred while archiving projects");
            }
        }

        /// <summary>
        /// Get project notes
        /// </summary>
        [HttpGet("{projectId}/notes")]
        public async Task<ActionResult<List<ProjectNoteDto>>> GetProjectNotes(int projectId)
        {
            try
            {
                var notes = await _projectService.GetProjectNotesAsync(projectId);
                return Ok(notes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project notes");
                return StatusCode(500, "An error occurred while retrieving project notes");
            }
        }

        /// <summary>
        /// Create a new project note
        /// </summary>
        [HttpPost("{projectId}/notes")]
        public async Task<ActionResult<ProjectNoteDto>> CreateProjectNote(int projectId, [FromBody] CreateProjectNoteRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var note = await _projectService.CreateProjectNoteAsync(projectId, createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetProjectNotes), new { projectId = projectId }, note);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project note");
                return StatusCode(500, "An error occurred while creating the project note");
            }
        }

        /// <summary>
        /// Delete a project note
        /// </summary>
        [HttpDelete("notes/{noteId}")]
        public async Task<ActionResult> DeleteProjectNote(int noteId)
        {
            try
            {
                var success = await _projectService.DeleteProjectNoteAsync(noteId, GetCurrentUser());
                if (!success)
                {
                    return NotFound($"Note with ID {noteId} not found");
                }
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                // Only the note creator may delete — surface 403, not 500.
                _logger.LogWarning(ex, "Forbidden delete of project note {NoteId}", noteId);
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project note");
                return StatusCode(500, "An error occurred while deleting the project note");
            }
        }


        /// <summary>
        /// Get project activity
        /// </summary>
        [HttpGet("{projectId}/activity")]
        public async Task<ActionResult<List<ProjectActivityDto>>> GetProjectActivity(int projectId)
        {
            try
            {
                var activities = await _projectService.GetProjectActivityAsync(projectId);
                return Ok(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project activity");
                return StatusCode(500, "An error occurred while retrieving project activity");
            }
        }

        [HttpGet("{projectId}/links")]
        public async Task<ActionResult<ProjectLinksDto>> GetProjectLinks(int projectId)
        {
            try
            {
                var links = await _projectService.GetProjectLinksAsync(projectId);
                return Ok(links);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Project not found while getting links for project {ProjectId}", projectId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project links");
                return StatusCode(500, "An error occurred while retrieving project links");
            }
        }

        [HttpPost("{projectId}/links")]
        public async Task<ActionResult<ProjectLinksDto>> LinkEntityToProject(int projectId, [FromBody] LinkProjectEntityRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                if (dto.EntityId <= 0) return BadRequest("entityId must be greater than 0");
                if (string.IsNullOrWhiteSpace(dto.EntityType)) return BadRequest("entityType is required");
                var links = await _projectService.LinkEntityToProjectAsync(projectId, dto, GetCurrentUser());
                return Ok(links);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Entity or project not found while linking to project {ProjectId}", projectId);
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid link request for project {ProjectId}", projectId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking entity to project");
                return StatusCode(500, "An error occurred while linking entity");
            }
        }

        [HttpDelete("{projectId}/links/{entityType}/{entityId:int}")]
        public async Task<ActionResult<ProjectLinksDto>> UnlinkEntityFromProject(int projectId, string entityType, int entityId)
        {
            try
            {
                if (entityId <= 0) return BadRequest("entityId must be greater than 0");
                if (string.IsNullOrWhiteSpace(entityType)) return BadRequest("entityType is required");
                var links = await _projectService.UnlinkEntityFromProjectAsync(projectId, entityType, entityId, GetCurrentUser());
                return Ok(links);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Entity or project not found while unlinking from project {ProjectId}", projectId);
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid unlink request for project {ProjectId}", projectId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlinking entity from project");
                return StatusCode(500, "An error occurred while unlinking entity");
            }
        }

        [HttpGet("settings")]
        public async Task<ActionResult<ProjectSettingsDto>> GetProjectSettings()
        {
            try
            {
                var settings = await _projectService.GetProjectSettingsAsync();
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project settings");
                return StatusCode(500, "An error occurred while retrieving project settings");
            }
        }

        [HttpPut("settings")]
        public async Task<ActionResult<ProjectSettingsDto>> UpdateProjectSettings([FromBody] ProjectSettingsDto dto)
        {
            try
            {
                var settings = await _projectService.UpdateProjectSettingsAsync(dto, GetCurrentUser());
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project settings");
                return StatusCode(500, "An error occurred while updating project settings");
            }
        }

        [HttpGet("{projectId}/team-members")]
        public async Task<ActionResult<List<int>>> GetTeamMembers(int projectId)
        {
            try { return Ok(await _projectService.GetTeamMembersAsync(projectId)); }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception ex) { _logger.LogError(ex, "Error getting team members"); return StatusCode(500, "Error"); }
        }

        [HttpPost("{projectId}/team-members")]
        public async Task<ActionResult> AssignTeamMember(int projectId, [FromBody] AssignTeamMemberRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                if (dto.UserId <= 0) return BadRequest("userId must be greater than 0");
                await _projectService.AssignTeamMemberAsync(projectId, dto, GetCurrentUser());
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { _logger.LogError(ex, "Error assigning team member"); return StatusCode(500, "Error"); }
        }

        [HttpDelete("{projectId}/team-members")]
        public async Task<ActionResult> RemoveTeamMember(int projectId, [FromBody] RemoveTeamMemberRequestDto dto)
        {
            try
            {
                if (dto.UserId <= 0) return BadRequest("userId must be greater than 0");
                var ok = await _projectService.RemoveTeamMemberAsync(projectId, dto.UserId, GetCurrentUser());
                if (!ok) return NotFound("Team member not found on project");
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { _logger.LogError(ex, "Error removing team member"); return StatusCode(500, "Error"); }
        }


        private string GetCurrentUser()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst("email")?.Value ?? 
                   "system";
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
