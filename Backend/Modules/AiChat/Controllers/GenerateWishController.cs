using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.AiChat.DTOs;
using MyApi.Modules.AiChat.Services;
using System.Security.Claims;

namespace MyApi.Modules.AiChat.Controllers
{
    /// <summary>
    /// Endpoint to generate AI responses via the local Ollama LLM.
    /// POST /api/GenerateWish — non-streaming JSON response
    /// POST /api/GenerateWish/stream — SSE streaming response
    /// AllowAnonymous: allows calls without JWT for development/testing.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class GenerateWishController : ControllerBase
    {
        private readonly IOllamaService _ollamaService;
        private readonly ILogger<GenerateWishController> _logger;

        public GenerateWishController(IOllamaService ollamaService, ILogger<GenerateWishController> logger)
        {
            _ollamaService = ollamaService;
            _logger = logger;
        }

        /// <summary>
        /// Generate AI response (non-streaming). Accepts prompt or messages[].
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ProducesResponseType(typeof(GenerateWishResponseDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [ProducesResponseType(503)]
        public async Task<ActionResult<GenerateWishResponseDto>> Generate([FromBody] GenerateWishRequestDto dto)
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("📥 [GenerateWish] Incoming request — user={UserId}, hasPrompt={HasPrompt}, msgCount={MsgCount}, model={Model}, temp={Temp}, maxTokens={MaxTokens}, stream={Stream}",
                userId,
                !string.IsNullOrWhiteSpace(dto.Prompt),
                dto.Messages?.Count ?? 0,
                dto.Model ?? "(default)",
                dto.Temperature?.ToString() ?? "(default)",
                dto.MaxTokens?.ToString() ?? "(default)",
                dto.Stream);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("⚠️ [GenerateWish] Invalid model state — user={UserId}, errors={Errors}",
                    userId, string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(dto.Prompt) && (dto.Messages == null || dto.Messages.Count == 0))
            {
                _logger.LogWarning("⚠️ [GenerateWish] Empty request — no prompt and no messages — user={UserId}", userId);
                return BadRequest(new GenerateWishResponseDto
                {
                    Success = false,
                    Error = "Either 'prompt' or 'messages' must be provided"
                });
            }

            // Log message contents for debugging
            if (dto.Messages != null)
            {
                for (int i = 0; i < dto.Messages.Count; i++)
                {
                    var msg = dto.Messages[i];
                    _logger.LogDebug("📝 [GenerateWish] Message[{Index}] role={Role}, contentLen={Len}, preview=\"{Preview}\"",
                        i, msg.Role, msg.Content?.Length ?? 0, msg.Content?.Substring(0, Math.Min(msg.Content.Length, 120)) ?? "(null)");
                }
            }

            try
            {
                _logger.LogInformation("🚀 [GenerateWish] Calling OllamaService.GenerateAsync — user={UserId}", userId);
                var result = await _ollamaService.GenerateAsync(dto, userId);

                _logger.LogInformation("📤 [GenerateWish] Result — user={UserId}, success={Success}, model={Model}, responseLen={Len}, durationNs={DurationNs}, error={Error}",
                    userId, result.Success, result.Model, result.Response?.Length ?? 0, result.TotalDurationNs, result.Error ?? "(none)");

                if (!result.Success)
                {
                    if (result.Error?.Contains("Cannot reach") == true ||
                        result.Error?.Contains("timed out") == true)
                    {
                        _logger.LogWarning("🔌 [GenerateWish] Service unavailable — user={UserId}, error={Error}", userId, result.Error);
                        return StatusCode(503, result);
                    }
                    _logger.LogWarning("❌ [GenerateWish] LLM error — user={UserId}, error={Error}", userId, result.Error);
                    return StatusCode(502, result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [GenerateWish] Unhandled exception — user={UserId}, exType={ExType}", userId, ex.GetType().Name);
                return StatusCode(500, new GenerateWishResponseDto
                {
                    Success = false,
                    Error = "Internal server error"
                });
            }
        }

        /// <summary>
        /// Stream AI response via SSE. Accepts prompt or messages[].
        /// </summary>
        [HttpPost("stream")]
        [AllowAnonymous]
        public async Task Stream([FromBody] GenerateWishRequestDto dto)
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("📥 [StreamChat] Incoming stream request — user={UserId}, hasPrompt={HasPrompt}, msgCount={MsgCount}, model={Model}, temp={Temp}, maxTokens={MaxTokens}",
                userId,
                !string.IsNullOrWhiteSpace(dto.Prompt),
                dto.Messages?.Count ?? 0,
                dto.Model ?? "(default)",
                dto.Temperature?.ToString() ?? "(default)",
                dto.MaxTokens?.ToString() ?? "(default)");

            if (string.IsNullOrWhiteSpace(dto.Prompt) && (dto.Messages == null || dto.Messages.Count == 0))
            {
                _logger.LogWarning("⚠️ [StreamChat] Empty request — no prompt and no messages — user={UserId}", userId);
                Response.StatusCode = 400;
                await Response.WriteAsync("{\"error\":\"Either 'prompt' or 'messages' must be provided\"}");
                return;
            }

            // Log message contents for debugging
            if (dto.Messages != null)
            {
                for (int i = 0; i < dto.Messages.Count; i++)
                {
                    var msg = dto.Messages[i];
                    _logger.LogDebug("📝 [StreamChat] Message[{Index}] role={Role}, contentLen={Len}, preview=\"{Preview}\"",
                        i, msg.Role, msg.Content?.Length ?? 0, msg.Content?.Substring(0, Math.Min(msg.Content.Length, 120)) ?? "(null)");
                }
            }

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            _logger.LogInformation("🚀 [StreamChat] Starting SSE stream — user={UserId}", userId);
            await _ollamaService.StreamChatAsync(dto, userId, Response.Body, HttpContext.RequestAborted);
            _logger.LogInformation("🏁 [StreamChat] SSE stream ended — user={UserId}", userId);
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                   User.FindFirst(ClaimTypes.Email)?.Value ??
                   User.FindFirst("sub")?.Value ??
                   "anonymous";
        }
    }
}
