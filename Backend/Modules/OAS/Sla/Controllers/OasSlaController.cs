using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Sla.DTOs;
using MyApi.Modules.OAS.Sla.Services;

namespace MyApi.Modules.OAS.Sla.Controllers;

[Route("api/oas/sla")]
[OasPluginGate("OA0003SHOPFLOOR")]
public class OasSlaController : OasControllerBase
{
    private readonly IOasSlaService _service;
    public OasSlaController(IOasSlaService service) => _service = service;

    // Verified: only Reports.tsx (web) calls GetRules; the responder-availability endpoints below stay unrestricted since eventStore.ts (shared with InterventionInbox, mobile+web) depends on them.
    [HttpGet("rules")] [OasWorkspace("web")] public async Task<ActionResult<IReadOnlyList<OasSlaRuleDto>>> GetRules() => Ok(await _service.GetRulesAsync(CurrentTenantId));

    [HttpPost("rules")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<ActionResult<OasSlaRuleDto>> CreateRule([FromBody] OasSlaRuleRequestDto request) => Ok(await _service.CreateRuleAsync(CurrentTenantId, request));

    [HttpPut("rules/{id}")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] OasSlaRuleRequestDto request)
        => await _service.UpdateRuleAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpDelete("rules/{id}")] [OasAuthorize(Roles = "admin")] [OasWorkspace("web")]
    public async Task<IActionResult> DeleteRule(Guid id)
        => await _service.DeleteRuleAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();

    [HttpGet("escalations")]
    public async Task<ActionResult<IReadOnlyList<OasEscalationDto>>> GetEscalations([FromQuery] Guid? eventId) => Ok(await _service.GetEscalationsAsync(CurrentTenantId, eventId));

    [HttpPost("escalations/{id}/ack")]
    public async Task<IActionResult> AckEscalation(Guid id)
        => await _service.AckEscalationAsync(CurrentTenantId, CurrentOasUserId, id) ? Ok(new { success = true }) : NotFound();
}

[Route("api/oas/responder-availability")]
[OasPluginGate("OA0005INTERVENTIONS")]
public class OasResponderAvailabilityController : OasControllerBase
{
    private readonly IOasSlaService _service;
    public OasResponderAvailabilityController(IOasSlaService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasResponderAvailabilityDto>>> GetAll() => Ok(await _service.GetAvailabilityAsync(CurrentTenantId));

    /// <summary>v15: a non-privileged caller may only set their OWN availability — never anyone else's, closing the "shared hardcoded identity" bug the old client had.</summary>
    [HttpPut("{profileId}")]
    public async Task<IActionResult> Set(Guid profileId, [FromBody] OasResponderAvailabilityRequestDto request)
    {
        var isPrivileged = CurrentOasRole is "admin" or "supervisor";
        if (!isPrivileged && profileId != CurrentOasUserId)
        {
            return Problem(statusCode: 403, title: "can_only_set_own_availability");
        }
        return await _service.SetAvailabilityAsync(CurrentTenantId, profileId, request) ? Ok(new { success = true }) : NotFound();
    }
}
