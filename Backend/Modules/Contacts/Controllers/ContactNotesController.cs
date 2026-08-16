using MyApi.Modules.Contacts.DTOs;
using MyApi.Modules.Contacts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MyApi.Modules.Contacts.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ContactNotesController : ControllerBase
    {
        private readonly IContactNoteService _contactNoteService;
        private readonly ILogger<ContactNotesController> _logger;

        public ContactNotesController(IContactNoteService contactNoteService, ILogger<ContactNotesController> logger)
        {
            _contactNoteService = contactNoteService;
            _logger = logger;
        }

        /// <summary>
        /// Get all notes for a specific contact
        /// </summary>
        [HttpGet("contact/{contactId}")]
        public async Task<ActionResult<ContactNoteListResponseDto>> GetNotesByContactId(int contactId)
        {
            try
            {
                var result = await _contactNoteService.GetNotesByContactIdAsync(contactId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notes for contact {ContactId}", contactId);
                return StatusCode(500, "An error occurred while retrieving notes");
            }
        }

        /// <summary>
        /// Get note by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ContactNoteDto>> GetNote(int id)
        {
            try
            {
                var note = await _contactNoteService.GetNoteByIdAsync(id);
                
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
        public async Task<ActionResult<ContactNoteDto>> CreateNote([FromBody] CreateContactNoteRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var note = await _contactNoteService.CreateNoteAsync(createDto, currentUser);

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
        public async Task<ActionResult<ContactNoteDto>> UpdateNote(int id, [FromBody] UpdateContactNoteRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var note = await _contactNoteService.UpdateNoteAsync(id, updateDto);

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
                var success = await _contactNoteService.DeleteNoteAsync(id);

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
            return User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst("email")?.Value ?? 
                   "system";
        }
    }
}
