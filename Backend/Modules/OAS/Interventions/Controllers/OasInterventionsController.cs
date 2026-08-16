using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Interventions.DTOs;
using MyApi.Modules.OAS.Interventions.Services;

namespace MyApi.Modules.OAS.Interventions.Controllers;

[Route("api/oas/interventions")]
[OasPluginGate("OA0005INTERVENTIONS")]
public class OasInterventionsController : OasControllerBase
{
    private readonly IOasInterventionService _service;
    public OasInterventionsController(IOasInterventionService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasInterventionDto>>> GetAll() => Ok(await _service.GetAllAsync(CurrentTenantId));

    [HttpGet("inbox")]
    public async Task<ActionResult<IReadOnlyList<OasInterventionDto>>> Inbox() => Ok(await _service.GetInboxAsync(CurrentTenantId, CurrentOasUserId));

    [HttpPost]
    public async Task<ActionResult<OasInterventionDto>> Create([FromBody] OasCreateInterventionRequestDto request) => Ok(await _service.CreateAsync(CurrentTenantId, request));

    [HttpPost("{id}/assign")]
    public async Task<IActionResult> Assign(Guid id)
    {
        var (success, error) = await _service.AssignAsync(CurrentTenantId, CurrentOasUserId, id);
        if (!success) return error == "not_found" ? NotFound() : Problem(statusCode: 409, title: error);
        return Ok(new { success = true });
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> Start(Guid id)
    {
        var (success, error) = await _service.StartAsync(CurrentTenantId, id);
        if (!success) return error == "not_found" ? NotFound() : Problem(statusCode: 409, title: error);
        return Ok(new { success = true });
    }

    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(Guid id)
    {
        var (success, error) = await _service.CloseAsync(CurrentTenantId, CurrentOasUserId, id);
        if (!success) return error == "not_found" ? NotFound() : Problem(statusCode: 409, title: error);
        return Ok(new { success = true });
    }
}
