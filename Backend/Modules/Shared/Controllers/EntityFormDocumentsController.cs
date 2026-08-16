using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Shared.DTOs;
using MyApi.Modules.Shared.Services;
using System.Security.Claims;

namespace MyApi.Modules.Shared.Controllers
{
    /// <summary>
    /// API Controller for Entity Form Documents
    /// Manages dynamic form documents attached to offers, sales, service orders, and dispatches
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EntityFormDocumentsController : ControllerBase
    {
        private readonly IEntityFormDocumentService _service;
        private readonly ILogger<EntityFormDocumentsController> _logger;

        public EntityFormDocumentsController(
            IEntityFormDocumentService service,
            ILogger<EntityFormDocumentsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Get all form documents for a specific entity (offer/sale/serviceorder/dispatch)
        /// </summary>
        /// <param name="entityType">Entity type: 'offer', 'sale', 'serviceorder', or 'dispatch'</param>
        /// <param name="entityId">Entity ID</param>
        [HttpGet("{entityType}/{entityId}")]
        public async Task<ActionResult<IEnumerable<EntityFormDocumentDto>>> GetByEntity(
            string entityType, int entityId)
        {
            try
            {
                var documents = await _service.GetByEntityAsync(entityType, entityId);
                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching form documents for {EntityType} {EntityId}", 
                    entityType, entityId);
                return StatusCode(500, new { message = "Error fetching form documents" });
            }
        }

        /// <summary>
        /// Get a single form document by ID
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<EntityFormDocumentDto>> GetById(int id)
        {
            try
            {
                var document = await _service.GetByIdAsync(id);
                if (document == null)
                {
                    return NotFound(new { message = $"Form document with ID {id} not found" });
                }
                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching form document {Id}", id);
                return StatusCode(500, new { message = "Error fetching form document" });
            }
        }

        /// <summary>
        /// Create a new form document
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<EntityFormDocumentDto>> Create(
            [FromBody] CreateEntityFormDocumentDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var document = await _service.CreateAsync(dto, userId);
                return CreatedAtAction(nameof(GetById), new { id = document.Id }, document);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating form document");
                return StatusCode(500, new { message = "Error creating form document" });
            }
        }

        /// <summary>
        /// Update an existing form document
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<EntityFormDocumentDto>> Update(
            int id, [FromBody] UpdateEntityFormDocumentDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var document = await _service.UpdateAsync(id, dto, userId);
                return Ok(document);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating form document {Id}", id);
                return StatusCode(500, new { message = "Error updating form document" });
            }
        }

        /// <summary>
        /// Delete a form document (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var result = await _service.DeleteAsync(id, userId);
                if (!result)
                {
                    return NotFound(new { message = $"Form document with ID {id} not found" });
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting form document {Id}", id);
                return StatusCode(500, new { message = "Error deleting form document" });
            }
        }

        /// <summary>
        /// Copy all form documents from one entity to another
        /// Used during workflow transitions (offer to sale, sale to service order, etc.)
        /// </summary>
        [HttpPost("copy")]
        public async Task<ActionResult> CopyDocuments([FromBody] CopyDocumentsDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var copiedCount = await _service.CopyDocumentsToEntityAsync(
                    dto.SourceEntityType, dto.SourceEntityId,
                    dto.TargetEntityType, dto.TargetEntityId,
                    userId);
                
                return Ok(new { copiedCount, message = $"Successfully copied {copiedCount} form document(s)" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying form documents from {SourceType} {SourceId} to {TargetType} {TargetId}",
                    dto.SourceEntityType, dto.SourceEntityId, dto.TargetEntityType, dto.TargetEntityId);
                return StatusCode(500, new { message = "Error copying form documents" });
            }
        }
    }

    /// <summary>
    /// DTO for copying form documents between entities
    /// </summary>
    public class CopyDocumentsDto
    {
        public string SourceEntityType { get; set; } = string.Empty;
        public int SourceEntityId { get; set; }
        public string TargetEntityType { get; set; } = string.Empty;
        public int TargetEntityId { get; set; }
    }
}
