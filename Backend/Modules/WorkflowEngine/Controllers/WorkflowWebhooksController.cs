using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.WorkflowEngine.Services;
using System.Text.Json;

namespace MyApi.Modules.WorkflowEngine.Controllers
{
    /// <summary>
    /// Inbound webhook endpoint that lets external systems start a workflow.
    /// Triggers must be registered with EntityType="webhook", FromStatus=path slug,
    /// and (optionally) ToStatus=shared secret token.
    /// </summary>
    [ApiController]
    [Route("api/workflow-webhooks")]
    [AllowAnonymous] // authenticated via per-trigger token in ToStatus
    public class WorkflowWebhooksController : ControllerBase
    {
        private readonly IWorkflowTriggerService _triggerService;
        private readonly ILogger<WorkflowWebhooksController> _logger;

        public WorkflowWebhooksController(
            IWorkflowTriggerService triggerService,
            ILogger<WorkflowWebhooksController> logger)
        {
            _triggerService = triggerService;
            _logger = logger;
        }

        /// <summary>
        /// Fire a workflow from an external system. The path slug identifies the
        /// trigger; an optional `X-Webhook-Token` header (or `?token=` query) is
        /// validated against the configured per-trigger token.
        /// </summary>
        [HttpPost("{path}")]
        public async Task<IActionResult> Receive(
            string path,
            [FromQuery] string? token,
            [FromBody] JsonElement payload)
        {
            // Header overrides query string when both are present.
            if (Request.Headers.TryGetValue("X-Webhook-Token", out var headerToken) && !string.IsNullOrEmpty(headerToken))
            {
                token = headerToken.ToString();
            }

            _logger.LogInformation("[WORKFLOW-WEBHOOK] Inbound webhook for path '{Path}'", path);

            try
            {
                var executionId = await _triggerService.TriggerWebhookAsync(path, token, payload);
                if (executionId == null)
                {
                    return NotFound(new { message = "No active webhook trigger for that path, or invalid token." });
                }
                return Accepted(new { executionId, path, status = "started" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW-WEBHOOK] Failed to handle webhook for path '{Path}'", path);
                return StatusCode(500, new { message = "Webhook processing failed", error = ex.Message });
            }
        }
    }
}
