using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Cadences.DTOs;
using MyApi.Modules.OAS.Cadences.Services;
using MyApi.Modules.OAS.Common;

namespace MyApi.Modules.OAS.Cadences.Controllers;

[Route("api/oas/cadences")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasCadencesController : OasControllerBase
{
    private readonly IOasCadenceService _service;
    public OasCadencesController(IOasCadenceService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasCadenceDto>>> GetAll() => Ok(await _service.GetAllAsync(CurrentTenantId));

    [HttpGet("current")]
    public async Task<ActionResult<OasCadenceDto>> Current([FromQuery] Guid postId, [FromQuery] Guid productId)
    {
        var dto = await _service.GetCurrentAsync(CurrentTenantId, postId, productId);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasCadenceDto>> CreateVersion([FromBody] OasCadenceRequestDto request)
        => Ok(await _service.UpsertNewVersionAsync(CurrentTenantId, CurrentOasUserIdOrNull, request));

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasCadenceRequestDto request)
        => await _service.UpdateAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpDelete("{id}")] [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();

    [HttpGet("{id}/history")]
    public async Task<ActionResult<IReadOnlyList<OasCadenceVersionDto>>> History(Guid id) => Ok(await _service.GetHistoryAsync(CurrentTenantId, id));

    private Guid? CurrentOasUserIdOrNull
    {
        get
        {
            var claim = User.FindFirst("oas_user_id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
