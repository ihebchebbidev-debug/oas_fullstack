using MyApi.Modules.Installations.DTOs;
using MyApi.Modules.Installations.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MyApi.Modules.Installations.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InstallationNotesController : ControllerBase
    {
        private readonly IInstallationNoteService _installationNoteService;
        private readonly ILogger<InstallationNotesController> _logger;

        public InstallationNotesController(IInstallationNoteService installationNoteService, ILogger<InstallationNotesController> logger)
        {
            _installationNoteService = installationNoteService;
            _logger = logger;
        }

        /// <summary>
        /// Get all notes for a specific installation
        /// </summary>
        [HttpGet("installation/{installationId}")]
        public async Task<ActionResult<InstallationNoteListResponseDto>> GetNotesByInstallationId(int installationId)
        {
            try
            {
                var result = await _installationNoteService.GetNotesByInstallationIdAsync(installationId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notes for installation {InstallationId}", installationId);
                return StatusCode(500, "An error occurred while retrieving notes");
            }
        }

        /// <summary>
        /// Get note by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<InstallationNoteDto>> GetNote(int id)
        {
            try
            {
                var note = await _installationNoteService.GetNoteByIdAsync(id);
                
                if (note == null)
                {
                    return NotFound($"Note with ID {id} not found");
                }

                return Ok(note);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting note with ID {NoteId}", id);
                return StatusCode(500, "An error occurred while retrieving the note");
            }
        }

        /// <summary>
        /// Create a new note
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<InstallationNoteDto>> CreateNote([FromBody] CreateInstallationNoteRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var note = await _installationNoteService.CreateNoteAsync(createDto, currentUser);

                return CreatedAtAction(nameof(GetNote), new { id = note.Id }, note);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while creating note");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating note");
                return StatusCode(500, "An error occurred while creating the note");
            }
        }

        /// <summary>
        /// Update an existing note
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<InstallationNoteDto>> UpdateNote(int id, [FromBody] UpdateInstallationNoteRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var note = await _installationNoteService.UpdateNoteAsync(id, updateDto);

                if (note == null)
                {
                    return NotFound($"Note with ID {id} not found");
                }

                return Ok(note);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating note with ID {NoteId}", id);
                return StatusCode(500, "An error occurred while updating the note");
            }
        }

        /// <summary>
        /// Delete a note
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteNote(int id)
        {
            try
            {
                var success = await _installationNoteService.DeleteNoteAsync(id);

                if (!success)
                {
                    return NotFound($"Note with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting note with ID {NoteId}", id);
                return StatusCode(500, "An error occurred while deleting the note");
            }
        }

        private string GetCurrentUser()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                   User.FindFirst("UserId")?.Value ?? 
                   User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst("email")?.Value ?? 
                   "system";
        }
    }
}
