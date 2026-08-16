using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.AiChat.DTOs;
using MyApi.Modules.AiChat.Services;
using System.Security.Claims;

namespace MyApi.Modules.AiChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AiChatController : ControllerBase
    {
        private readonly IAiChatService _aiChatService;
        private readonly ILogger<AiChatController> _logger;

        public AiChatController(IAiChatService aiChatService, ILogger<AiChatController> logger)
        {
            _aiChatService = aiChatService;
            _logger = logger;
        }

        #region Conversation Endpoints

        /// <summary>
        /// Get all conversations for the current user
        /// </summary>
        [HttpGet("conversations")]
        public async Task<ActionResult<AiConversationListResponseDto>> GetConversations(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool includeArchived = false)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _aiChatService.GetConversationsAsync(userId, pageNumber, pageSize, includeArchived);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations");
                return StatusCode(500, "An error occurred while retrieving conversations");
            }
        }

        /// <summary>
        /// Get a specific conversation with messages
        /// </summary>
        [HttpGet("conversations/{id}")]
        public async Task<ActionResult<AiConversationResponseDto>> GetConversation(
            int id,
            [FromQuery] bool includeMessages = true)
        {
            var userId = GetCurrentUserId();

            // Validate route param early — surfaces bad client calls clearly
            if (id <= 0)
            {
                _logger.LogWarning(
                    "GetConversation called with invalid id={ConversationId} by user {UserId}",
                    id, userId);
                return BadRequest(new { error = "INVALID_ID", message = $"Conversation id must be positive (got {id})." });
            }

            try
            {
                _logger.LogDebug(
                    "GetConversation start id={ConversationId} userId={UserId} includeMessages={IncludeMessages}",
                    id, userId, includeMessages);

                var conversation = await _aiChatService.GetConversationByIdAsync(id, userId, includeMessages);

                if (conversation == null)
                {
                    _logger.LogInformation(
                        "Conversation {ConversationId} not found for user {UserId} (deleted, wrong owner, or never existed)",
                        id, userId);
                    return NotFound(new { error = "NOT_FOUND", message = $"Conversation with ID {id} not found" });
                }

                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error getting conversation {ConversationId} for user {UserId} (includeMessages={IncludeMessages})",
                    id, userId, includeMessages);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while retrieving the conversation" });
            }
        }

        /// <summary>
        /// Create a new conversation
        /// </summary>
        [HttpPost("conversations")]
        public async Task<ActionResult<AiConversationResponseDto>> CreateConversation([FromBody] CreateConversationRequestDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var conversation = await _aiChatService.CreateConversationAsync(dto, userId);
                return CreatedAtAction(nameof(GetConversation), new { id = conversation.Id }, conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation");
                return StatusCode(500, "An error occurred while creating the conversation");
            }
        }

        /// <summary>
        /// Update a conversation (title, archive, pin)
        /// </summary>
        [HttpPatch("conversations/{id}")]
        public async Task<ActionResult<AiConversationResponseDto>> UpdateConversation(
            int id,
            [FromBody] UpdateConversationRequestDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var conversation = await _aiChatService.UpdateConversationAsync(id, dto, userId);

                if (conversation == null)
                {
                    return NotFound($"Conversation with ID {id} not found");
                }

                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating conversation {ConversationId}", id);
                return StatusCode(500, "An error occurred while updating the conversation");
            }
        }

        /// <summary>
        /// Delete a conversation (soft delete)
        /// </summary>
        [HttpDelete("conversations/{id}")]
        public async Task<ActionResult> DeleteConversation(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _aiChatService.DeleteConversationAsync(id, userId);

                if (!success)
                {
                    return NotFound($"Conversation with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation {ConversationId}", id);
                return StatusCode(500, "An error occurred while deleting the conversation");
            }
        }

        /// <summary>
        /// Delete all conversations for the current user
        /// </summary>
        [HttpDelete("conversations")]
        public async Task<ActionResult> DeleteAllConversations()
        {
            try
            {
                var userId = GetCurrentUserId();
                await _aiChatService.DeleteAllConversationsAsync(userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all conversations for user");
                return StatusCode(500, "An error occurred while deleting all conversations");
            }
        }

        /// <summary>
        /// Archive/unarchive a conversation
        /// </summary>
        [HttpPost("conversations/{id}/archive")]
        public async Task<ActionResult> ArchiveConversation(int id, [FromQuery] bool archive = true)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _aiChatService.ArchiveConversationAsync(id, userId, archive);

                if (!success)
                {
                    return NotFound($"Conversation with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving conversation {ConversationId}", id);
                return StatusCode(500, "An error occurred while archiving the conversation");
            }
        }

        /// <summary>
        /// Pin/unpin a conversation
        /// </summary>
        [HttpPost("conversations/{id}/pin")]
        public async Task<ActionResult> PinConversation(int id, [FromQuery] bool pin = true)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _aiChatService.PinConversationAsync(id, userId, pin);

                if (!success)
                {
                    return NotFound($"Conversation with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning conversation {ConversationId}", id);
                return StatusCode(500, "An error occurred while pinning the conversation");
            }
        }

        #endregion

        #region Message Endpoints

        /// <summary>
        /// Add a message to a conversation
        /// </summary>
        [HttpPost("messages")]
        public async Task<ActionResult<AiMessageResponseDto>> AddMessage([FromBody] AddMessageRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = GetCurrentUserId();
                var message = await _aiChatService.AddMessageAsync(dto, userId);
                return Created($"/api/aichat/messages/{message.Id}", message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding message to conversation {ConversationId}", dto.ConversationId);
                return StatusCode(500, "An error occurred while adding the message");
            }
        }

        /// <summary>
        /// Add multiple messages to a conversation (bulk)
        /// </summary>
        [HttpPost("messages/bulk")]
        public async Task<ActionResult<List<AiMessageResponseDto>>> AddMessages([FromBody] BulkAddMessagesRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = GetCurrentUserId();
                var messages = await _aiChatService.AddMessagesAsync(dto, userId);
                return Ok(messages);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding messages to conversation {ConversationId}", dto.ConversationId);
                return StatusCode(500, "An error occurred while adding messages");
            }
        }

        /// <summary>
        /// Update message feedback (like/dislike)
        /// </summary>
        [HttpPatch("messages/{id}/feedback")]
        public async Task<ActionResult<AiMessageResponseDto>> UpdateMessageFeedback(
            int id,
            [FromBody] UpdateMessageFeedbackRequestDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var message = await _aiChatService.UpdateMessageFeedbackAsync(id, dto, userId);

                if (message == null)
                {
                    return NotFound($"Message with ID {id} not found");
                }

                return Ok(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating message feedback {MessageId}", id);
                return StatusCode(500, "An error occurred while updating the message feedback");
            }
        }

        /// <summary>
        /// Delete a message
        /// </summary>
        [HttpDelete("messages/{id}")]
        public async Task<ActionResult> DeleteMessage(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _aiChatService.DeleteMessageAsync(id, userId);

                if (!success)
                {
                    return NotFound($"Message with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId}", id);
                return StatusCode(500, "An error occurred while deleting the message");
            }
        }

        #endregion

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                   User.FindFirst(ClaimTypes.Email)?.Value ??
                   User.FindFirst("sub")?.Value ??
                   "anonymous";
        }
    }
}
