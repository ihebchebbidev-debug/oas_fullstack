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
    public class TaskAttachmentsController : ControllerBase
    {
        private readonly ITaskAttachmentService _attachmentService;
        private readonly ILogger<TaskAttachmentsController> _logger;

        public TaskAttachmentsController(ITaskAttachmentService attachmentService, ILogger<TaskAttachmentsController> logger)
        {
            _attachmentService = attachmentService;
            _logger = logger;
        }

        /// <summary>
        /// Get attachments for a project task
        /// </summary>
        [HttpGet("project-task/{projectTaskId}")]
        public async Task<ActionResult<TaskAttachmentListResponseDto>> GetProjectTaskAttachments(int projectTaskId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var attachments = await _attachmentService.GetTaskAttachmentsAsync(projectTaskId: projectTaskId, pageNumber: pageNumber, pageSize: pageSize);
                return Ok(attachments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attachments for project task {TaskId}", projectTaskId);
                return StatusCode(500, "An error occurred while retrieving attachments");
            }
        }

        /// <summary>
        /// Get attachments for a daily task
        /// </summary>
        [HttpGet("daily-task/{dailyTaskId}")]
        public async Task<ActionResult<TaskAttachmentListResponseDto>> GetDailyTaskAttachments(int dailyTaskId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var attachments = await _attachmentService.GetTaskAttachmentsAsync(dailyTaskId: dailyTaskId, pageNumber: pageNumber, pageSize: pageSize);
                return Ok(attachments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attachments for daily task {TaskId}", dailyTaskId);
                return StatusCode(500, "An error occurred while retrieving attachments");
            }
        }

        /// <summary>
        /// Get attachment by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<TaskAttachmentResponseDto>> GetAttachment(int id)
        {
            try
            {
                var attachment = await _attachmentService.GetAttachmentByIdAsync(id);
                
                if (attachment == null)
                {
                    return NotFound($"Attachment with ID {id} not found");
                }

                return Ok(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attachment with ID {AttachmentId}", id);
                return StatusCode(500, "An error occurred while retrieving the attachment");
            }
        }

        /// <summary>
        /// Upload a new attachment
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<TaskAttachmentResponseDto>> CreateAttachment([FromBody] CreateTaskAttachmentRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var attachment = await _attachmentService.CreateAttachmentAsync(createDto, currentUser);

                return CreatedAtAction(nameof(GetAttachment), new { id = attachment.Id }, attachment);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while creating attachment");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attachment");
                return StatusCode(500, "An error occurred while creating the attachment");
            }
        }

        /// <summary>
        /// Update an existing attachment
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<TaskAttachmentResponseDto>> UpdateAttachment(int id, [FromBody] UpdateTaskAttachmentRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var attachment = await _attachmentService.UpdateAttachmentAsync(id, updateDto, currentUser);

                if (attachment == null)
                {
                    return NotFound($"Attachment with ID {id} not found");
                }

                return Ok(attachment);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to update attachment {AttachmentId}", id);
                return Forbid("You can only edit your own attachments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attachment with ID {AttachmentId}", id);
                return StatusCode(500, "An error occurred while updating the attachment");
            }
        }

        /// <summary>
        /// Delete an attachment
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteAttachment(int id)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var success = await _attachmentService.DeleteAttachmentAsync(id, currentUser);

                if (!success)
                {
                    return NotFound($"Attachment with ID {id} not found");
                }

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to delete attachment {AttachmentId}", id);
                return Forbid("You can only delete your own attachments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attachment with ID {AttachmentId}", id);
                return StatusCode(500, "An error occurred while deleting the attachment");
            }
        }

        /// <summary>
        /// Search attachments
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<TaskAttachmentListResponseDto>> SearchAttachments([FromQuery] TaskAttachmentSearchRequestDto searchRequest)
        {
            try
            {
                var result = await _attachmentService.SearchAttachmentsAsync(searchRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching attachments");
                return StatusCode(500, "An error occurred while searching attachments");
            }
        }

        /// <summary>
        /// Get image attachments
        /// </summary>
        [HttpGet("images")]
        public async Task<ActionResult<TaskAttachmentListResponseDto>> GetImageAttachments([FromQuery] int? projectTaskId = null, [FromQuery] int? dailyTaskId = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var attachments = await _attachmentService.GetImageAttachmentsAsync(projectTaskId, dailyTaskId, pageNumber, pageSize);
                return Ok(attachments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting image attachments");
                return StatusCode(500, "An error occurred while retrieving image attachments");
            }
        }

        /// <summary>
        /// Get document attachments
        /// </summary>
        [HttpGet("documents")]
        public async Task<ActionResult<TaskAttachmentListResponseDto>> GetDocumentAttachments([FromQuery] int? projectTaskId = null, [FromQuery] int? dailyTaskId = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var attachments = await _attachmentService.GetDocumentAttachmentsAsync(projectTaskId, dailyTaskId, pageNumber, pageSize);
                return Ok(attachments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document attachments");
                return StatusCode(500, "An error occurred while retrieving document attachments");
            }
        }

        /// <summary>
        /// Bulk delete attachments
        /// </summary>
        [HttpDelete("bulk")]
        public async Task<ActionResult> BulkDeleteAttachments([FromBody] BulkDeleteTaskAttachmentsDto bulkDeleteDto)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var success = await _attachmentService.BulkDeleteAttachmentsAsync(bulkDeleteDto, currentUser);

                if (!success)
                {
                    return BadRequest("Failed to delete attachments");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk attachment deletion");
                return StatusCode(500, "An error occurred during bulk deletion");
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
