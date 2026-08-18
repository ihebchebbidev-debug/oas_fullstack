using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Causes.DTOs;
using MyApi.Modules.OAS.Causes.Services;
using MyApi.Modules.OAS.Common;

namespace MyApi.Modules.OAS.Causes.Controllers;

[Route("api/oas/causes")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasCausesController : OasControllerBase
{
    private readonly IOasCauseService _service;
    public OasCausesController(IOasCauseService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasCauseDto>>> GetTree() => Ok(await _service.GetTreeAsync(CurrentTenantId));

    [HttpGet("usage")]
    public async Task<ActionResult<IReadOnlyList<OasCauseUsageDto>>> Usage() => Ok(await _service.GetUsageAsync(CurrentTenantId));

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> Create([FromBody] OasCauseRequestDto request)
    {
        var (success, error, dto) = await _service.CreateAsync(CurrentTenantId, request);
        return success ? Ok(dto) : BadRequest(new { error });
    }

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasCauseRequestDto request)
        => await _service.UpdateAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpPut("{id}/kind")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> SetKind(Guid id, [FromBody] OasCauseKindRequestDto request)
        => await _service.SetKindAsync(CurrentTenantId, id, request.EventType) ? Ok(new { success = true }) : NotFound();

    [HttpPut("{id}/criticality")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> SetCriticality(Guid id, [FromBody] OasCauseCriticalityRequestDto request)
        => await _service.SetCriticalityAsync(CurrentTenantId, id, request.Criticality) ? Ok(new { success = true }) : NotFound();

    [HttpPut("{id}/active")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] OasCauseActiveRequestDto request)
        => await _service.SetActiveAsync(CurrentTenantId, id, request.IsActive) ? Ok(new { success = true }) : NotFound();

    [HttpPost("{id}/children")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> AddChild(Guid id, [FromBody] OasCauseRequestDto request)
    {
        var (success, error, dto) = await _service.AddChildAsync(CurrentTenantId, id, request);
        return success ? Ok(dto) : BadRequest(new { error });
    }

    [HttpDelete("{id}/children/{childId}")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> RemoveChild(Guid id, Guid childId)
        => await _service.RemoveChildAsync(CurrentTenantId, id, childId) ? Ok(new { success = true }) : NotFound();

    [HttpDelete("{id}")] [OasAuthorize(Roles = "admin")] [OasWorkspace("web")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();
}

[Route("api/oas/cause-proposals")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasCauseProposalsController : OasControllerBase
{
    private readonly IOasCauseService _service;
    public OasCauseProposalsController(IOasCauseService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasCauseProposalDto>>> GetAll() => Ok(await _service.GetProposalsAsync(CurrentTenantId));

    /// <summary>No role restriction by design: any operator can flag a missing cause from the mobile app; only admin/supervisor can approve it via Review below.</summary>
    [HttpPost]
    public async Task<ActionResult<OasCauseProposalDto>> Create([FromBody] OasCauseProposalRequestDto request)
        => Ok(await _service.CreateProposalAsync(CurrentTenantId, CurrentOasUserId, request));

    [HttpPost("{id}/review")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> Review(Guid id, [FromBody] OasCauseProposalReviewRequestDto request)
    {
        var (success, error) = await _service.ReviewProposalAsync(CurrentTenantId, id, CurrentOasUserId, request);
        if (!success) return error == "not_found" ? NotFound() : BadRequest(new { error });
        return Ok(new { success = true });
    }
}
