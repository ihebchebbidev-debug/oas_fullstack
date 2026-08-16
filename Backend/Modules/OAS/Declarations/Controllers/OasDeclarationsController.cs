using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Declarations.DTOs;
using MyApi.Modules.OAS.Declarations.Services;
using MyApi.Modules.OAS.Events.DTOs;
using MyApi.Modules.OAS.Events.Services;

namespace MyApi.Modules.OAS.Declarations.Controllers;

/// <summary>Spec §6.1 Déclarations. No [OasWorkspace] gate — both console (CorrectionsPanel) and mobile (DeclareProduction/HistoryPage) call these, and the 10-min correction window (BL-045) must be enforced identically for both (v15: the console today skips this check, a bug not to replicate).</summary>
[Route("api/oas/declarations")]
[OasPluginGate("OA0004DECLARATIONS")]
public class OasDeclarationsController : OasControllerBase
{
    private readonly IOasDeclarationService _service;
    private readonly IOasEventService _events;
    public OasDeclarationsController(IOasDeclarationService service, IOasEventService events)
    {
        _service = service;
        _events = events;
    }

    [HttpPost("production")]
    public async Task<ActionResult<OasDeclarationDto>> CreateProduction([FromBody] OasProductionDeclarationRequestDto request)
        => Ok(await _service.CreateProductionAsync(CurrentTenantId, CurrentOasUserId, request));

    [HttpPost("scrap")]
    public async Task<ActionResult<OasDeclarationDto>> CreateScrap([FromBody] OasScrapDeclarationRequestDto request)
        => Ok(await _service.CreateScrapAsync(CurrentTenantId, CurrentOasUserId, request));

    [HttpPut("{id}/correct")]
    public async Task<IActionResult> Correct(Guid id, [FromBody] OasCorrectDeclarationRequestDto request)
    {
        var (success, error, dto) = await _service.CorrectAsync(CurrentTenantId, CurrentOasUserId, CurrentOasRole, id, request);
        if (!success)
        {
            return error switch
            {
                "not_found" => NotFound(),
                "not_your_declaration" => Problem(statusCode: 403, title: error),
                "correction_window_expired" => Problem(statusCode: 409, title: error),
                _ => BadRequest(new { error }),
            };
        }
        return Ok(dto);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OasDeclarationDto>>> GetAll([FromQuery] Guid? postId, [FromQuery] Guid? sessionId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
        => Ok(await _service.GetAsync(CurrentTenantId, postId, sessionId, from, to));

    [HttpGet("{id}")]
    public async Task<ActionResult<OasDeclarationDto>> GetOne(Guid id)
    {
        var dto = await _service.GetOneAsync(CurrentTenantId, id);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// `declareStop()` (session.ts:404) is functionally an event creation,
    /// not a row in oas_declarations (oas_declaration_kind has no 'stop'
    /// value) — this is a thin, literal-route-matching wrapper over
    /// IOasEventService so /mobile/stop's actual URL contract
    /// (POST /declarations/stop) is honored without duplicating the event
    /// state machine.
    /// </summary>
    [HttpPost("stop")]
    public async Task<ActionResult<OasEventDto>> DeclareStop([FromBody] OasCreateEventRequestDto request)
        => Ok(await _events.CreateAsync(CurrentTenantId, CurrentOasUserId, request));

    [HttpPost("stop/{clientEventId}/close")]
    public async Task<IActionResult> CloseStop(Guid clientEventId, [FromBody] OasCloseEventRequestDto request)
    {
        var existing = await _events.GetByClientEventIdAsync(CurrentTenantId, clientEventId);
        if (existing is null) return NotFound();

        var (success, error, dto) = await _events.CloseAsync(CurrentTenantId, CurrentOasUserId, existing.Id, request);
        if (!success) return error == "not_found" ? NotFound() : Problem(statusCode: 409, title: error);
        return Ok(dto);
    }
}
