using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Lookups.DTOs;
using MyApi.Modules.Lookups.Services;

namespace MyApi.Modules.Lookups.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class LookupsController : ControllerBase
    {
        private readonly ILookupService _lookupService;
        private readonly ILogger<LookupsController> _logger;

        public LookupsController(ILookupService lookupService, ILogger<LookupsController> logger)
        {
            _lookupService = lookupService;
            _logger = logger;
        }

        // Article Categories
        [HttpGet("article-categories")]
        public async Task<ActionResult<LookupListResponseDto>> GetArticleCategories()
        {
            try
            {
                var result = await _lookupService.GetArticleCategoriesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving article categories");
                return StatusCode(500, "An error occurred while retrieving article categories.");
            }
        }

        [HttpGet("article-categories/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetArticleCategory(int id)
        {
            try
            {
                var result = await _lookupService.GetArticleCategoryByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving article category with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the article category.");
            }
        }

        [HttpPost("article-categories")]
        public async Task<ActionResult<LookupItemDto>> CreateArticleCategory([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateArticleCategoryAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetArticleCategory), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating article category");
                return StatusCode(500, "An error occurred while creating the article category.");
            }
        }

        [HttpPut("article-categories/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateArticleCategory(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateArticleCategoryAsync(id, updateDto, GetCurrentUser());
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating article category with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the article category.");
            }
        }

        [HttpDelete("article-categories/{id}")]
        public async Task<IActionResult> DeleteArticleCategory(int id)
        {
            try
            {
                var result = await _lookupService.DeleteArticleCategoryAsync(id, GetCurrentUser());
                if (!result)
                    return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting article category with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the article category.");
            }
        }

        // Article Groups
        [HttpGet("article-groups")]
        public async Task<ActionResult<LookupListResponseDto>> GetArticleGroups()
        {
            try
            {
                var result = await _lookupService.GetArticleGroupsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving article groups");
                return StatusCode(500, "An error occurred while retrieving article groups.");
            }
        }

        [HttpGet("article-groups/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetArticleGroup(int id)
        {
            try
            {
                var result = await _lookupService.GetArticleGroupByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving article group with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the article group.");
            }
        }

        [HttpPost("article-groups")]
        public async Task<ActionResult<LookupItemDto>> CreateArticleGroup([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateArticleGroupAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetArticleGroup), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating article group");
                return StatusCode(500, "An error occurred while creating the article group.");
            }
        }

        [HttpPut("article-groups/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateArticleGroup(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateArticleGroupAsync(id, updateDto, GetCurrentUser());
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating article group with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the article group.");
            }
        }

        [HttpDelete("article-groups/{id}")]
        public async Task<IActionResult> DeleteArticleGroup(int id)
        {
            try
            {
                var result = await _lookupService.DeleteArticleGroupAsync(id, GetCurrentUser());
                if (!result)
                    return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting article group with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the article group.");
            }
        }

        // Document Types
        [HttpGet("document-types")]
        public async Task<ActionResult<LookupListResponseDto>> GetDocumentTypes()
        {
            try
            {
                var result = await _lookupService.GetDocumentTypesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document types");
                return StatusCode(500, "An error occurred while retrieving document types.");
            }
        }

        [HttpGet("document-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetDocumentType(int id)
        {
            try
            {
                var result = await _lookupService.GetDocumentTypeByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the document type.");
            }
        }

        [HttpPost("document-types")]
        public async Task<ActionResult<LookupItemDto>> CreateDocumentType([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateDocumentTypeAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetDocumentType), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating document type");
                return StatusCode(500, "An error occurred while creating the document type.");
            }
        }

        [HttpPut("document-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateDocumentType(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateDocumentTypeAsync(id, updateDto, GetCurrentUser());
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the document type.");
            }
        }

        [HttpDelete("document-types/{id}")]
        public async Task<IActionResult> DeleteDocumentType(int id)
        {
            try
            {
                var result = await _lookupService.DeleteDocumentTypeAsync(id, GetCurrentUser());
                if (!result)
                    return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the document type.");
            }
        }

        // Article Statuses
        [HttpGet("article-statuses")]
        public async Task<ActionResult<LookupListResponseDto>> GetArticleStatuses()
        {
            try
            {
                var result = await _lookupService.GetArticleStatusesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving article statuses");
                return StatusCode(500, "An error occurred while retrieving article statuses.");
            }
        }

        [HttpPost("article-statuses")]
        public async Task<ActionResult<LookupItemDto>> CreateArticleStatus([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateArticleStatusAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetArticleStatuses), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating article status");
                return StatusCode(500, "An error occurred while creating the article status.");
            }
        }

        [HttpPut("article-statuses/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateArticleStatus(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateArticleStatusAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating article status with ID: {Id}", id);
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("article-statuses/{id}")]
        public async Task<IActionResult> DeleteArticleStatus(int id)
        {
            try
            {
                var result = await _lookupService.DeleteArticleStatusAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting article status with ID: {Id}", id);
                return StatusCode(500, "An error occurred.");
            }
        }

        // Service Categories
        [HttpGet("service-categories")]
        public async Task<ActionResult<LookupListResponseDto>> GetServiceCategories()
        {
            try
            {
                var result = await _lookupService.GetServiceCategoriesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving service categories");
                return StatusCode(500, "An error occurred while retrieving service categories.");
            }
        }

        [HttpPost("service-categories")]
        public async Task<ActionResult<LookupItemDto>> CreateServiceCategory([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateServiceCategoryAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetServiceCategories), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service category");
                return StatusCode(500, "An error occurred while creating the service category.");
            }
        }

        [HttpPut("service-categories/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateServiceCategory(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateServiceCategoryAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service category");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("service-categories/{id}")]
        public async Task<IActionResult> DeleteServiceCategory(int id)
        {
            try
            {
                var result = await _lookupService.DeleteServiceCategoryAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting service category");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Task Statuses
        [HttpGet("task-statuses")]
        public async Task<ActionResult<LookupListResponseDto>> GetTaskStatuses()
        {
            try
            {
                var result = await _lookupService.GetTaskStatusesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving task statuses");
                return StatusCode(500, "An error occurred while retrieving task statuses.");
            }
        }

        [HttpPost("task-statuses")]
        public async Task<ActionResult<LookupItemDto>> CreateTaskStatus([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateTaskStatusAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetTaskStatuses), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task status");
                return StatusCode(500, "An error occurred while creating the task status.");
            }
        }

        [HttpPut("task-statuses/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateTaskStatus(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateTaskStatusAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task status");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("task-statuses/{id}")]
        public async Task<IActionResult> DeleteTaskStatus(int id)
        {
            try
            {
                var result = await _lookupService.DeleteTaskStatusAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task status");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Event Types
        [HttpGet("event-types")]
        public async Task<ActionResult<LookupListResponseDto>> GetEventTypes()
        {
            try
            {
                var result = await _lookupService.GetEventTypesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving event types");
                return StatusCode(500, "An error occurred while retrieving event types.");
            }
        }

        [HttpPost("event-types")]
        public async Task<ActionResult<LookupItemDto>> CreateEventType([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateEventTypeAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetEventTypes), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating event type");
                return StatusCode(500, "An error occurred while creating the event type.");
            }
        }

        [HttpPut("event-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateEventType(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateEventTypeAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event type");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("event-types/{id}")]
        public async Task<IActionResult> DeleteEventType(int id)
        {
            try
            {
                var result = await _lookupService.DeleteEventTypeAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting event type");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Priorities
        [HttpGet("priorities")]
        public async Task<ActionResult<LookupListResponseDto>> GetPriorities()
        {
            try
            {
                var result = await _lookupService.GetPrioritiesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving priorities");
                return StatusCode(500, "An error occurred while retrieving priorities.");
            }
        }

        [HttpPost("priorities")]
        public async Task<ActionResult<LookupItemDto>> CreatePriority([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreatePriorityAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetPriorities), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating priority");
                return StatusCode(500, "An error occurred while creating the priority.");
            }
        }

        [HttpPut("priorities/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdatePriority(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdatePriorityAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating priority");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("priorities/{id}")]
        public async Task<IActionResult> DeletePriority(int id)
        {
            try
            {
                var result = await _lookupService.DeletePriorityAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting priority");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Technician Statuses
        [HttpGet("technician-statuses")]
        public async Task<ActionResult<LookupListResponseDto>> GetTechnicianStatuses()
        {
            try
            {
                var result = await _lookupService.GetTechnicianStatusesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving technician statuses");
                return StatusCode(500, "An error occurred while retrieving technician statuses.");
            }
        }

        [HttpPost("technician-statuses")]
        public async Task<ActionResult<LookupItemDto>> CreateTechnicianStatus([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateTechnicianStatusAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetTechnicianStatuses), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating technician status");
                return StatusCode(500, "An error occurred while creating the technician status.");
            }
        }

        [HttpPut("technician-statuses/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateTechnicianStatus(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateTechnicianStatusAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating technician status");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("technician-statuses/{id}")]
        public async Task<IActionResult> DeleteTechnicianStatus(int id)
        {
            try
            {
                var result = await _lookupService.DeleteTechnicianStatusAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting technician status");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Leave Types
        [HttpGet("leave-types")]
        public async Task<ActionResult<LookupListResponseDto>> GetLeaveTypes()
        {
            try
            {
                var result = await _lookupService.GetLeaveTypesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leave types");
                return StatusCode(500, "An error occurred while retrieving leave types.");
            }
        }

        [HttpPost("leave-types")]
        public async Task<ActionResult<LookupItemDto>> CreateLeaveType([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateLeaveTypeAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetLeaveTypes), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating leave type");
                return StatusCode(500, "An error occurred while creating the leave type.");
            }
        }

        [HttpPut("leave-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateLeaveType(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateLeaveTypeAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating leave type");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("leave-types/{id}")]
        public async Task<IActionResult> DeleteLeaveType(int id)
        {
            try
            {
                var result = await _lookupService.DeleteLeaveTypeAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting leave type");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Project Statuses
        [HttpGet("project-statuses")]
        public async Task<ActionResult<LookupListResponseDto>> GetProjectStatuses()
        {
            try
            {
                var result = await _lookupService.GetProjectStatusesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project statuses");
                return StatusCode(500, "An error occurred while retrieving project statuses.");
            }
        }

        [HttpPost("project-statuses")]
        public async Task<ActionResult<LookupItemDto>> CreateProjectStatus([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateProjectStatusAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetProjectStatuses), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project status");
                return StatusCode(500, "An error occurred while creating the project status.");
            }
        }

        [HttpPut("project-statuses/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateProjectStatus(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateProjectStatusAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project status");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("project-statuses/{id}")]
        public async Task<IActionResult> DeleteProjectStatus(int id)
        {
            try
            {
                var result = await _lookupService.DeleteProjectStatusAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project status");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Project Types
        [HttpGet("project-types")]
        public async Task<ActionResult<LookupListResponseDto>> GetProjectTypes()
        {
            try
            {
                var result = await _lookupService.GetProjectTypesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project types");
                return StatusCode(500, "An error occurred while retrieving project types.");
            }
        }

        [HttpPost("project-types")]
        public async Task<ActionResult<LookupItemDto>> CreateProjectType([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateProjectTypeAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetProjectTypes), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project type");
                return StatusCode(500, "An error occurred while creating the project type.");
            }
        }

        [HttpPut("project-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateProjectType(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateProjectTypeAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project type");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("project-types/{id}")]
        public async Task<IActionResult> DeleteProjectType(int id)
        {
            try
            {
                var result = await _lookupService.DeleteProjectTypeAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project type");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Offer Statuses
        [HttpGet("offer-statuses")]
        public async Task<ActionResult<LookupListResponseDto>> GetOfferStatuses()
        {
            try
            {
                var result = await _lookupService.GetOfferStatusesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving offer statuses");
                return StatusCode(500, "An error occurred while retrieving offer statuses.");
            }
        }

        [HttpPost("offer-statuses")]
        public async Task<ActionResult<LookupItemDto>> CreateOfferStatus([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateOfferStatusAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetOfferStatuses), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating offer status");
                return StatusCode(500, "An error occurred while creating the offer status.");
            }
        }

        [HttpPut("offer-statuses/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateOfferStatus(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateOfferStatusAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating offer status");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("offer-statuses/{id}")]
        public async Task<IActionResult> DeleteOfferStatus(int id)
        {
            try
            {
                var result = await _lookupService.DeleteOfferStatusAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting offer status");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Sale Statuses
        [HttpGet("sale-statuses")]
        public async Task<ActionResult<LookupListResponseDto>> GetSaleStatuses()
        {
            try
            {
                var result = await _lookupService.GetSaleStatusesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sale statuses");
                return StatusCode(500, "An error occurred while retrieving sale statuses.");
            }
        }

        [HttpPost("sale-statuses")]
        public async Task<ActionResult<LookupItemDto>> CreateSaleStatus([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateSaleStatusAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetSaleStatuses), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sale status");
                return StatusCode(500, "An error occurred while creating the sale status.");
            }
        }

        [HttpPut("sale-statuses/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateSaleStatus(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateSaleStatusAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sale status");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("sale-statuses/{id}")]
        public async Task<IActionResult> DeleteSaleStatus(int id)
        {
            try
            {
                var result = await _lookupService.DeleteSaleStatusAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sale status");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Service Order Statuses
        [HttpGet("service-order-statuses")]
        public async Task<ActionResult<LookupListResponseDto>> GetServiceOrderStatuses()
        {
            try
            {
                var result = await _lookupService.GetServiceOrderStatusesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving service order statuses");
                return StatusCode(500, "An error occurred while retrieving service order statuses.");
            }
        }

        [HttpPost("service-order-statuses")]
        public async Task<ActionResult<LookupItemDto>> CreateServiceOrderStatus([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateServiceOrderStatusAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetServiceOrderStatuses), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service order status");
                return StatusCode(500, "An error occurred while creating the service order status.");
            }
        }

        [HttpPut("service-order-statuses/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateServiceOrderStatus(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateServiceOrderStatusAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service order status");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("service-order-statuses/{id}")]
        public async Task<IActionResult> DeleteServiceOrderStatus(int id)
        {
            try
            {
                var result = await _lookupService.DeleteServiceOrderStatusAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting service order status");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Dispatch Statuses
        [HttpGet("dispatch-statuses")]
        public async Task<ActionResult<LookupListResponseDto>> GetDispatchStatuses()
        {
            try
            {
                var result = await _lookupService.GetDispatchStatusesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dispatch statuses");
                return StatusCode(500, "An error occurred while retrieving dispatch statuses.");
            }
        }

        [HttpPost("dispatch-statuses")]
        public async Task<ActionResult<LookupItemDto>> CreateDispatchStatus([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateDispatchStatusAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetDispatchStatuses), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating dispatch status");
                return StatusCode(500, "An error occurred while creating the dispatch status.");
            }
        }

        [HttpPut("dispatch-statuses/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateDispatchStatus(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateDispatchStatusAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating dispatch status");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("dispatch-statuses/{id}")]
        public async Task<IActionResult> DeleteDispatchStatus(int id)
        {
            try
            {
                var result = await _lookupService.DeleteDispatchStatusAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting dispatch status");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Offer Categories
        [HttpGet("offer-categories")]
        public async Task<ActionResult<LookupListResponseDto>> GetOfferCategories()
        {
            try
            {
                var result = await _lookupService.GetOfferCategoriesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving offer categories");
                return StatusCode(500, "An error occurred while retrieving offer categories.");
            }
        }

        [HttpGet("offer-categories/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetOfferCategory(int id)
        {
            try
            {
                var result = await _lookupService.GetOfferCategoryByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving offer category with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the offer category.");
            }
        }

        [HttpPost("offer-categories")]
        public async Task<ActionResult<LookupItemDto>> CreateOfferCategory([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateOfferCategoryAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetOfferCategory), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating offer category");
                return StatusCode(500, "An error occurred while creating the offer category.");
            }
        }

        [HttpPut("offer-categories/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateOfferCategory(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateOfferCategoryAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating offer category");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("offer-categories/{id}")]
        public async Task<IActionResult> DeleteOfferCategory(int id)
        {
            try
            {
                var result = await _lookupService.DeleteOfferCategoryAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting offer category");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Offer Sources
        [HttpGet("offer-sources")]
        public async Task<ActionResult<LookupListResponseDto>> GetOfferSources()
        {
            try
            {
                var result = await _lookupService.GetOfferSourcesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving offer sources");
                return StatusCode(500, "An error occurred while retrieving offer sources.");
            }
        }

        [HttpGet("offer-sources/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetOfferSource(int id)
        {
            try
            {
                var result = await _lookupService.GetOfferSourceByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving offer source with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the offer source.");
            }
        }

        [HttpPost("offer-sources")]
        public async Task<ActionResult<LookupItemDto>> CreateOfferSource([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateOfferSourceAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetOfferSource), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating offer source");
                return StatusCode(500, "An error occurred while creating the offer source.");
            }
        }

        [HttpPut("offer-sources/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateOfferSource(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateOfferSourceAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating offer source");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("offer-sources/{id}")]
        public async Task<IActionResult> DeleteOfferSource(int id)
        {
            try
            {
                var result = await _lookupService.DeleteOfferSourceAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting offer source");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Skills (lookup type)
        [HttpGet("skills")]
        public async Task<ActionResult<LookupListResponseDto>> GetSkills()
        {
            try
            {
                var result = await _lookupService.GetSkillsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving skills");
                return StatusCode(500, "An error occurred while retrieving skills.");
            }
        }

        [HttpPost("skills")]
        public async Task<ActionResult<LookupItemDto>> CreateSkill([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateSkillAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetSkills), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating skill");
                return StatusCode(500, "An error occurred while creating the skill.");
            }
        }

        [HttpPut("skills/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateSkill(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateSkillAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating skill with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the skill.");
            }
        }

        [HttpDelete("skills/{id}")]
        public async Task<IActionResult> DeleteSkill(int id)
        {
            try
            {
                var result = await _lookupService.DeleteSkillAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting skill with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the skill.");
            }
        }

        // Installation Types
        [HttpGet("installation-types")]
        public async Task<ActionResult<LookupListResponseDto>> GetInstallationTypes()
        {
            try
            {
                var result = await _lookupService.GetInstallationTypesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving installation types");
                return StatusCode(500, "An error occurred while retrieving installation types.");
            }
        }

        [HttpGet("installation-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetInstallationType(int id)
        {
            try
            {
                var result = await _lookupService.GetInstallationTypeByIdAsync(id);
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving installation type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the installation type.");
            }
        }

        [HttpPost("installation-types")]
        public async Task<ActionResult<LookupItemDto>> CreateInstallationType([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateInstallationTypeAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetInstallationType), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating installation type");
                return StatusCode(500, "An error occurred while creating the installation type.");
            }
        }

        [HttpPut("installation-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateInstallationType(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateInstallationTypeAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating installation type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the installation type.");
            }
        }

        [HttpDelete("installation-types/{id}")]
        public async Task<IActionResult> DeleteInstallationType(int id)
        {
            try
            {
                var result = await _lookupService.DeleteInstallationTypeAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting installation type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the installation type.");
            }
        }

        // Installation Categories
        [HttpGet("installation-categories")]
        public async Task<ActionResult<LookupListResponseDto>> GetInstallationCategories()
        {
            try
            {
                var result = await _lookupService.GetInstallationCategoriesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving installation categories");
                return StatusCode(500, "An error occurred while retrieving installation categories.");
            }
        }

        [HttpGet("installation-categories/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetInstallationCategory(int id)
        {
            try
            {
                var result = await _lookupService.GetInstallationCategoryByIdAsync(id);
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving installation category with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the installation category.");
            }
        }

        [HttpPost("installation-categories")]
        public async Task<ActionResult<LookupItemDto>> CreateInstallationCategory([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateInstallationCategoryAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetInstallationCategory), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating installation category");
                return StatusCode(500, "An error occurred while creating the installation category.");
            }
        }

        [HttpPut("installation-categories/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateInstallationCategory(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateInstallationCategoryAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating installation category with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the installation category.");
            }
        }

        [HttpDelete("installation-categories/{id}")]
        public async Task<IActionResult> DeleteInstallationCategory(int id)
        {
            try
            {
                var result = await _lookupService.DeleteInstallationCategoryAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting installation category with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the installation category.");
            }
        }

        // Countries
        [HttpGet("countries")]
        public async Task<ActionResult<LookupListResponseDto>> GetCountries()
        {
            try
            {
                var result = await _lookupService.GetCountriesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving countries");
                return StatusCode(500, "An error occurred while retrieving countries.");
            }
        }

        [HttpPost("countries")]
        public async Task<ActionResult<LookupItemDto>> CreateCountry([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateCountryAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetCountries), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating country");
                return StatusCode(500, "An error occurred while creating the country.");
            }
        }

        [HttpPut("countries/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateCountry(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateCountryAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating country");
                return StatusCode(500, "An error occurred.");
            }
        }

        [HttpDelete("countries/{id}")]
        public async Task<IActionResult> DeleteCountry(int id)
        {
            try
            {
                var result = await _lookupService.DeleteCountryAsync(id, GetCurrentUser());
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting country");
                return StatusCode(500, "An error occurred.");
            }
        }

        // Locations
        [HttpGet("locations")]
        public async Task<ActionResult<LookupListResponseDto>> GetLocations()
        {
            try
            {
                var result = await _lookupService.GetLocationsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving locations");
                return StatusCode(500, "An error occurred while retrieving locations.");
            }
        }

        [HttpGet("locations/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetLocation(int id)
        {
            try
            {
                var result = await _lookupService.GetLocationByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving location with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the location.");
            }
        }

        [HttpPost("locations")]
        public async Task<ActionResult<LookupItemDto>> CreateLocation([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateLocationAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetLocation), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating location");
                return StatusCode(500, "An error occurred while creating the location.");
            }
        }

        [HttpPut("locations/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateLocation(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateLocationAsync(id, updateDto, GetCurrentUser());
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the location.");
            }
        }

        [HttpDelete("locations/{id}")]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            try
            {
                var result = await _lookupService.DeleteLocationAsync(id, GetCurrentUser());
                if (!result)
                    return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting location with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the location.");
            }
        }

        // Work Types
        [HttpGet("work-types")]
        public async Task<ActionResult<LookupListResponseDto>> GetWorkTypes()
        {
            try
            {
                var result = await _lookupService.GetWorkTypesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving work types");
                return StatusCode(500, "An error occurred while retrieving work types.");
            }
        }

        [HttpGet("work-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetWorkType(int id)
        {
            try
            {
                var result = await _lookupService.GetWorkTypeByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving work type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the work type.");
            }
        }

        [HttpPost("work-types")]
        public async Task<ActionResult<LookupItemDto>> CreateWorkType([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateWorkTypeAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetWorkType), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating work type");
                return StatusCode(500, "An error occurred while creating the work type.");
            }
        }

        [HttpPut("work-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateWorkType(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateWorkTypeAsync(id, updateDto, GetCurrentUser());
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating work type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the work type.");
            }
        }

        [HttpDelete("work-types/{id}")]
        public async Task<IActionResult> DeleteWorkType(int id)
        {
            try
            {
                var result = await _lookupService.DeleteWorkTypeAsync(id, GetCurrentUser());
                if (!result)
                    return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting work type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the work type.");
            }
        }

        // Expense Types
        [HttpGet("expense-types")]
        public async Task<ActionResult<LookupListResponseDto>> GetExpenseTypes()
        {
            try
            {
                var result = await _lookupService.GetExpenseTypesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expense types");
                return StatusCode(500, "An error occurred while retrieving expense types.");
            }
        }

        [HttpGet("expense-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetExpenseType(int id)
        {
            try
            {
                var result = await _lookupService.GetExpenseTypeByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expense type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the expense type.");
            }
        }

        [HttpPost("expense-types")]
        public async Task<ActionResult<LookupItemDto>> CreateExpenseType([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateExpenseTypeAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetExpenseType), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating expense type");
                return StatusCode(500, "An error occurred while creating the expense type.");
            }
        }

        [HttpPut("expense-types/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateExpenseType(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateExpenseTypeAsync(id, updateDto, GetCurrentUser());
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expense type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the expense type.");
            }
        }

        [HttpDelete("expense-types/{id}")]
        public async Task<IActionResult> DeleteExpenseType(int id)
        {
            try
            {
                var result = await _lookupService.DeleteExpenseTypeAsync(id, GetCurrentUser());
                if (!result)
                    return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expense type with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the expense type.");
            }
        }

        // Form Categories
        [HttpGet("form-categories")]
        public async Task<ActionResult<LookupListResponseDto>> GetFormCategories()
        {
            try
            {
                var result = await _lookupService.GetFormCategoriesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving form categories");
                return StatusCode(500, "An error occurred while retrieving form categories.");
            }
        }

        [HttpGet("form-categories/{id}")]
        public async Task<ActionResult<LookupItemDto>> GetFormCategory(int id)
        {
            try
            {
                var result = await _lookupService.GetFormCategoryByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving form category with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the form category.");
            }
        }

        [HttpPost("form-categories")]
        public async Task<ActionResult<LookupItemDto>> CreateFormCategory([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateFormCategoryAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetFormCategory), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating form category");
                return StatusCode(500, "An error occurred while creating the form category.");
            }
        }

        [HttpPut("form-categories/{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateFormCategory(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateFormCategoryAsync(id, updateDto, GetCurrentUser());
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating form category with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the form category.");
            }
        }

        [HttpDelete("form-categories/{id}")]
        public async Task<IActionResult> DeleteFormCategory(int id)
        {
            try
            {
                var result = await _lookupService.DeleteFormCategoryAsync(id, GetCurrentUser());
                if (!result)
                    return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting form category with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the form category.");
            }
        }

        // Currencies
        [HttpGet("currencies")]
        public async Task<ActionResult<CurrencyListResponseDto>> GetCurrencies()
        {
            try
            {
                var result = await _lookupService.GetCurrenciesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving currencies");
                return StatusCode(500, "An error occurred while retrieving currencies.");
            }
        }

        [HttpGet("currencies/{id}")]
        public async Task<ActionResult<CurrencyDto>> GetCurrency(int id)
        {
            try
            {
                var result = await _lookupService.GetCurrencyByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving currency with ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the currency.");
            }
        }

        [HttpPost("currencies")]
        public async Task<ActionResult<CurrencyDto>> CreateCurrency([FromBody] CreateCurrencyRequestDto createDto)
        {
            try
            {
                var result = await _lookupService.CreateCurrencyAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetCurrency), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating currency");
                return StatusCode(500, "An error occurred while creating the currency.");
            }
        }

        [HttpPut("currencies/{id}")]
        public async Task<ActionResult<CurrencyDto>> UpdateCurrency(int id, [FromBody] UpdateCurrencyRequestDto updateDto)
        {
            try
            {
                var result = await _lookupService.UpdateCurrencyAsync(id, updateDto, GetCurrentUser());
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating currency with ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the currency.");
            }
        }

        [HttpDelete("currencies/{id}")]
        public async Task<IActionResult> DeleteCurrency(int id)
        {
            try
            {
                var result = await _lookupService.DeleteCurrencyAsync(id, GetCurrentUser());
                if (!result)
                    return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting currency with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the currency.");
            }
        }

        private string GetCurrentUser()
        {
            return User.Identity?.Name ?? User.FindFirst("email")?.Value ?? "system";
        }
    }
}
