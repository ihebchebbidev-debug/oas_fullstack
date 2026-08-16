using MyApi.Modules.Articles.DTOs;
using MyApi.Modules.Articles.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MyApi.Modules.Articles.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ArticleNotesController : ControllerBase
    {
        private readonly IArticleNoteService _service;
        private readonly ILogger<ArticleNotesController> _logger;

        public ArticleNotesController(IArticleNoteService service, ILogger<ArticleNotesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("article/{articleId}")]
        public async Task<ActionResult<ArticleNoteListResponseDto>> GetByArticleId(int articleId)
        {
            try
            {
                var result = await _service.GetNotesByArticleIdAsync(articleId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notes for article {ArticleId}", articleId);
                return StatusCode(500, "An error occurred while retrieving notes");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ArticleNoteDto>> GetNote(int id)
        {
            var note = await _service.GetNoteByIdAsync(id);
            if (note == null) return NotFound($"Note with ID {id} not found");
            return Ok(note);
        }

        [HttpPost]
        public async Task<ActionResult<ArticleNoteDto>> Create([FromBody] CreateArticleNoteRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var user = GetCurrentUser();
                var note = await _service.CreateNoteAsync(dto, user);
                return CreatedAtAction(nameof(GetNote), new { id = note.Id }, note);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating article note");
                return StatusCode(500, "An error occurred while creating the note");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ArticleNoteDto>> Update(int id, [FromBody] UpdateArticleNoteRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var note = await _service.UpdateNoteAsync(id, dto);
            if (note == null) return NotFound($"Note with ID {id} not found");
            return Ok(note);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var ok = await _service.DeleteNoteAsync(id);
            if (!ok) return NotFound($"Note with ID {id} not found");
            return NoContent();
        }

        private string GetCurrentUser()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                   User.FindFirst(ClaimTypes.Email)?.Value ??
                   User.FindFirst(ClaimTypes.Name)?.Value ??
                   "system";
        }
    }
}