using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Teams.DTOs;
using MyApi.Modules.OAS.Teams.Services;

namespace MyApi.Modules.OAS.Teams.Controllers;

[Route("api/oas/teams")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasTeamsController : OasControllerBase
{
    private readonly IOasTeamService _service;
    public OasTeamsController(IOasTeamService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasTeamDto>>> GetAll() => Ok(await _service.GetAllAsync(CurrentTenantId));

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasTeamDto>> Create([FromBody] OasTeamRequestDto request) => Ok(await _service.CreateAsync(CurrentTenantId, request));

    [HttpPut("{id}/members")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> SetMembers(Guid id, [FromBody] OasTeamMembersRequestDto request)
        => await _service.SetMembersAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();
}
