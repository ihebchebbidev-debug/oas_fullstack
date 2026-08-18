using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Integrations.DTOs;
using MyApi.Modules.OAS.Integrations.Services;

namespace MyApi.Modules.OAS.Integrations.Controllers;

[Route("api/oas/integrations")]
[OasPluginGate("OA0011CONSOLE")]
[OasAuthorize(Roles = "admin,supervisor")]
public class OasIntegrationsController : OasControllerBase
{
    private readonly IOasIntegrationService _service;
    public OasIntegrationsController(IOasIntegrationService service) => _service = service;

    [HttpPost("webhooks/in")]
    public async Task<IActionResult> WebhookIn([FromBody] OasWebhookInRequest request)
    {
        var (ok, error, fannedOut) = await _service.ReceiveWebhookAsync(CurrentTenantId, request);
        return ok ? Ok(new { success = true, fannedOut }) : Problem(statusCode: 400, title: error);
    }

    // Console-management actions only — WebhookIn above is meant for an
    // external caller, not our own frontend, so it deliberately does NOT
    // get an OasWorkspace restriction.
    [HttpGet("endpoints")] [OasWorkspace("web")]
    public async Task<ActionResult<IReadOnlyList<OasIntegrationEndpointDto>>> GetEndpoints() => Ok(await _service.GetEndpointsAsync(CurrentTenantId));

    [HttpPost("endpoints")] [OasWorkspace("web")]
    public async Task<ActionResult<OasIntegrationEndpointDto>> CreateEndpoint([FromBody] OasIntegrationEndpointCreateRequest request)
        => Ok(await _service.CreateEndpointAsync(CurrentTenantId, request));

    [HttpPut("endpoints/{id}")] [OasWorkspace("web")]
    public async Task<IActionResult> UpdateEndpoint(Guid id, [FromBody] OasIntegrationEndpointUpdateRequest request)
        => await _service.UpdateEndpointAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpDelete("endpoints/{id}")] [OasAuthorize(Roles = "admin")] [OasWorkspace("web")]
    public async Task<IActionResult> DeleteEndpoint(Guid id)
        => await _service.DeleteEndpointAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();

    [HttpGet("outbox")] [OasWorkspace("web")]
    public async Task<ActionResult<IReadOnlyList<OasIntegrationOutboxDto>>> GetOutbox([FromQuery] string? status)
        => Ok(await _service.GetOutboxAsync(CurrentTenantId, status));
}
