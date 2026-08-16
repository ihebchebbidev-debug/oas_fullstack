using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Infrastructure;
using MyApi.Modules.DynamicForms.DTOs;
using MyApi.Modules.DynamicForms.Services;
using System.Security.Claims;

namespace MyApi.Modules.DynamicForms.Controllers
{
    /// <summary>
    /// API Controller for Dynamic Forms management
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DynamicFormsController : ControllerBase
    {
        private readonly IDynamicFormService _formService;
        private readonly ILogger<DynamicFormsController> _logger;

        public DynamicFormsController(
            IDynamicFormService formService,
            ILogger<DynamicFormsController> logger)
        {
            _formService = formService;
            _logger = logger;
        }

        /// <summary>
        /// Get all dynamic forms with optional filters
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResultDto<DynamicFormDto>>> GetAll([FromQuery] DynamicFormQueryParams queryParams)
        {
            try
            {
                var result = await _formService.GetAllAsync(queryParams);
                Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dynamic forms");
                return StatusCode(500, new { message = "Error fetching forms" });
            }
        }

        /// <summary>
        /// Get a single dynamic form by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<DynamicFormDto>> GetById(int id)
        {
            try
            {
                var form = await _formService.GetByIdAsync(id);
                if (form == null)
                {
                    return NotFound(new { message = $"Form with ID {id} not found" });
                }
                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching form {FormId}", id);
                return StatusCode(500, new { message = "Error fetching form" });
            }
        }

        /// <summary>
        /// Create a new dynamic form
        /// </summary>
        [HttpPost]
        [RequirePermission("dynamic_forms", "create")]
        public async Task<ActionResult<DynamicFormDto>> Create([FromBody] CreateDynamicFormDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var form = await _formService.CreateAsync(dto, userId);
                return CreatedAtAction(nameof(GetById), new { id = form.Id }, form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating dynamic form");
                return StatusCode(500, new { message = "Error creating form" });
            }
        }

        /// <summary>
        /// Update an existing dynamic form
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission("dynamic_forms", "update")]
        public async Task<ActionResult<DynamicFormDto>> Update(int id, [FromBody] UpdateDynamicFormDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var form = await _formService.UpdateAsync(id, dto, userId);
                return Ok(form);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Form with ID {id} not found" });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating form {FormId}", id);
                return StatusCode(500, new { message = "Error updating form" });
            }
        }

        /// <summary>
        /// Delete a dynamic form (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission("dynamic_forms", "delete")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var result = await _formService.DeleteAsync(id);
                if (!result)
                {
                    return NotFound(new { message = $"Form with ID {id} not found" });
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting form {FormId}", id);
                return StatusCode(500, new { message = "Error deleting form" });
            }
        }

        /// <summary>
        /// Duplicate an existing form
        /// </summary>
        [HttpPost("{id}/duplicate")]
        [RequirePermission("dynamic_forms", "create")]
        public async Task<ActionResult<DynamicFormDto>> Duplicate(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var form = await _formService.DuplicateAsync(id, userId);
                return CreatedAtAction(nameof(GetById), new { id = form.Id }, form);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Form with ID {id} not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error duplicating form {FormId}", id);
                return StatusCode(500, new { message = "Error duplicating form" });
            }
        }

        /// <summary>
        /// Change the status of a form (draft, released, archived)
        /// </summary>
        [HttpPost("{id}/status")]
        [RequirePermission("dynamic_forms", "update")]
        public async Task<ActionResult<DynamicFormDto>> ChangeStatus(int id, [FromBody] ChangeFormStatusDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var form = await _formService.ChangeStatusAsync(id, dto.Status, userId);
                return Ok(form);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Form with ID {id} not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing status for form {FormId}", id);
                return StatusCode(500, new { message = "Error changing form status" });
            }
        }

        /// <summary>
        /// Get all responses for a specific form
        /// </summary>
        [HttpGet("{id}/responses")]
        public async Task<ActionResult<PagedResultDto<DynamicFormResponseDto>>> GetResponses(int id, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            try
            {
                var result = await _formService.GetResponsesAsync(id, page, pageSize);
                Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching responses for form {FormId}", id);
                return StatusCode(500, new { message = "Error fetching responses" });
            }
        }

        /// <summary>
        /// Submit a response to a form
        /// </summary>
        [HttpPost("{id}/responses")]
        public async Task<ActionResult<DynamicFormResponseDto>> SubmitResponse(int id, [FromBody] SubmitFormResponseDto dto)
        {
            try
            {
                if (dto.FormId != id)
                {
                    dto.FormId = id;
                }
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var response = await _formService.SubmitResponseAsync(dto, userId);
                return CreatedAtAction(nameof(GetResponses), new { id = dto.FormId }, response);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Form with ID {id} not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting response for form {FormId}", id);
                return StatusCode(500, new { message = "Error submitting response" });
            }
        }

        /// <summary>
        /// Get response count for a form
        /// </summary>
        [HttpGet("{id}/responses/count")]
        public async Task<ActionResult<int>> GetResponseCount(int id)
        {
            try
            {
                var count = await _formService.GetResponseCountAsync(id);
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting response count for form {FormId}", id);
                return StatusCode(500, new { message = "Error getting response count" });
            }
        }
    }
}
