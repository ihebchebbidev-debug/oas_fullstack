using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Assignments.DTOs;
using MyApi.Modules.OAS.Assignments.Services;
using MyApi.Modules.OAS.Common;

namespace MyApi.Modules.OAS.Assignments.Controllers;

[Route("api/oas/assignments")]
[OasPluginGate("OA0007ASSIGNMENTS")]
public class OasAssignmentsController : OasControllerBase
{
    private readonly IOasAssignmentService _service;
    public OasAssignmentsController(IOasAssignmentService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OasAssignmentDto>>> GetAll([FromQuery] Guid shift, [FromQuery] DateOnly date)
        => Ok(await _service.GetAsync(CurrentTenantId, shift, date, onlyPublished: false));

    [HttpGet("published")]
    public async Task<ActionResult<IReadOnlyList<OasAssignmentDto>>> GetPublished([FromQuery] Guid shift, [FromQuery] DateOnly date)
        => Ok(await _service.GetAsync(CurrentTenantId, shift, date, onlyPublished: true));

    [HttpPut("{postId}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasAssignmentDto>> Upsert(Guid postId, [FromBody] OasAssignmentRequestDto request)
        => Ok(await _service.UpsertAsync(CurrentTenantId, CurrentOasUserId, postId, request));

    [HttpDelete("{postId}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Delete(Guid postId, [FromQuery] Guid shift, [FromQuery] DateOnly date)
        => await _service.DeleteAsync(CurrentTenantId, postId, shift, date) ? Ok(new { success = true }) : NotFound();

    [HttpPost("auto-fill")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> AutoFill([FromQuery] Guid shift, [FromQuery] DateOnly date)
        => Ok(new { filled = await _service.AutoFillAsync(CurrentTenantId, CurrentOasUserId, shift, date) });

    [HttpDelete] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> ClearAll([FromQuery] Guid shift, [FromQuery] DateOnly date)
        => Ok(new { removed = await _service.ClearAllAsync(CurrentTenantId, shift, date) });

    [HttpPost("publish")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Publish([FromQuery] Guid shift, [FromQuery] DateOnly date, [FromQuery] Guid? postId)
        => Ok(new { published = await _service.PublishAsync(CurrentTenantId, shift, date, postId) });

    [HttpGet("counts")]
    public async Task<ActionResult<OasAssignmentCountsDto>> Counts([FromQuery] Guid shift, [FromQuery] DateOnly date)
        => Ok(await _service.GetCountsAsync(CurrentTenantId, shift, date));

    [HttpGet("roster")]
    public async Task<ActionResult<IReadOnlyList<OasRosterEntryDto>>> Roster() => Ok(await _service.GetRosterAsync(CurrentTenantId));
}

[Route("api/oas/presence")]
[OasPluginGate("OA0007ASSIGNMENTS")]
public class OasPresenceController : OasControllerBase
{
    private readonly IOasAssignmentService _service;
    public OasPresenceController(IOasAssignmentService service) => _service = service;

    [HttpPut("{operatorId}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Set(Guid operatorId, [FromBody] OasPresenceRequestDto request)
        => await _service.SetPresenceAsync(CurrentTenantId, operatorId, request) ? Ok(new { success = true }) : NotFound();

    /// <summary>v10: mobile self-confirm only — a non-privileged caller may confirm their OWN presence, never a coworker's (same guard as OasResponderAvailabilityController.Set; this had none and let any authenticated user mark anyone "present").</summary>
    [HttpPost("{operatorId}/confirm")]
    public async Task<IActionResult> Confirm(Guid operatorId, [FromBody] OasPresenceConfirmRequestDto request)
    {
        var isPrivileged = CurrentOasRole is "admin" or "supervisor";
        if (!isPrivileged && operatorId != CurrentOasUserId)
        {
            return Problem(statusCode: 403, title: "can_only_confirm_own_presence");
        }
        return await _service.ConfirmPresenceAsync(CurrentTenantId, operatorId, request) ? Ok(new { success = true }) : NotFound();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OasPresenceDto>>> GetAll([FromQuery] Guid shift, [FromQuery] DateOnly date)
        => Ok(await _service.GetPresenceAsync(CurrentTenantId, shift, date));
}
