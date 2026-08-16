using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Hierarchy.DTOs;
using MyApi.Modules.OAS.Hierarchy.Services;

namespace MyApi.Modules.OAS.Hierarchy.Controllers;

[Route("api/oas/lines")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasLinesController : OasControllerBase
{
    private readonly IOasHierarchyService _service;
    public OasLinesController(IOasHierarchyService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasLineDto>>> GetAll([FromQuery] Guid? zoneId) => Ok(await _service.GetLinesAsync(CurrentTenantId, zoneId));

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasLineDto>> Create([FromBody] OasLineRequestDto request) => Ok(await _service.CreateLineAsync(CurrentTenantId, request));

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasLineRequestDto request)
        => await _service.UpdateLineAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpPost("{id}/archive")] [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> Archive(Guid id)
        => await _service.ArchiveLineAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();
}
