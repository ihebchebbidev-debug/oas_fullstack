using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Events.DTOs;
using MyApi.Modules.OAS.Events.Services;

namespace MyApi.Modules.OAS.Events.Controllers;

[Route("api/oas/events")]
[OasPluginGate("OA0003SHOPFLOOR")]
public class OasEventsController : OasControllerBase
{
    private readonly IOasEventService _service;
    public OasEventsController(IOasEventService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OasCreateEventRequestDto request)
        => await Respond(_service.CreateAsync(CurrentTenantId, CurrentOasUserId, request));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OasEventDto>>> GetAll(
        [FromQuery] string? kind, [FromQuery] string? stage, [FromQuery] string? q,
        [FromQuery] Guid? lineId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
        => Ok(await _service.GetAsync(CurrentTenantId, kind, stage, q, lineId, from, to));

    [HttpGet("{id}")]
    public async Task<ActionResult<OasEventDto>> GetOne(Guid id)
    {
        var dto = await _service.GetOneAsync(CurrentTenantId, id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("{id}/transitions")]
    public async Task<ActionResult<IReadOnlyList<OasEventTransitionDto>>> Transitions(Guid id) => Ok(await _service.GetTransitionsAsync(CurrentTenantId, id));

    [HttpPost("{id}/take")]
    public async Task<IActionResult> Take(Guid id) => await Respond(_service.TakeAsync(CurrentTenantId, CurrentOasUserId, id));

    [HttpPut("{id}/eta")]
    public async Task<IActionResult> Eta(Guid id, [FromBody] OasEtaRequestDto request) => await Respond(_service.SetEtaAsync(CurrentTenantId, id, request));

    [HttpPost("{id}/arrive")]
    public async Task<IActionResult> Arrive(Guid id) => await Respond(_service.ArriveAsync(CurrentTenantId, id));

    [HttpPost("{id}/advance")]
    public async Task<IActionResult> Advance(Guid id) => await Respond(_service.AdvanceAsync(CurrentTenantId, CurrentOasUserId, id));

    [HttpPost("{id}/ack")]
    public async Task<IActionResult> Ack(Guid id) => await Respond(_service.AckAsync(CurrentTenantId, CurrentOasUserId, id));

    [HttpPost("{id}/escalate")]
    public async Task<IActionResult> Escalate(Guid id) => await Respond(_service.EscalateAsync(CurrentTenantId, id));

    [HttpPost("{id}/requalify")]
    public async Task<IActionResult> Requalify(Guid id, [FromBody] OasRequalifyRequestDto request) => await Respond(_service.RequalifyAsync(CurrentTenantId, id, request));

    [HttpPost("{id}/decline")]
    public async Task<IActionResult> Decline(Guid id) => await Respond(_service.DeclineAsync(CurrentTenantId, id));

    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] OasCloseEventRequestDto request) => await Respond(_service.CloseAsync(CurrentTenantId, CurrentOasUserId, id, request));

    private IActionResult Respond((bool success, string? error, OasEventDto? dto) result)
    {
        if (result.success) return Ok(result.dto);
        return result.error switch
        {
            "not_found" => NotFound(),
            "event_closed" => Problem(statusCode: 409, title: "event_closed"),
            "event_already_resolved" => Problem(statusCode: 409, title: "event_already_resolved"),
            "open_blocking_event_exists" => Problem(statusCode: 409, title: "open_blocking_event_exists"),
            _ => BadRequest(new { error = result.error }),
        };
    }

    // Overload so `await Respond(_service.XAsync(...))` above works directly
    // on the awaited tuple without an extra `var` line per action.
    private async Task<IActionResult> Respond(Task<(bool success, string? error, OasEventDto? dto)> task) => Respond(await task);
}
