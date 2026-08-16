using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Hierarchy.DTOs;
using MyApi.Modules.OAS.Hierarchy.Services;

namespace MyApi.Modules.OAS.Hierarchy.Controllers;

[Route("api/oas/zones")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasZonesController : OasControllerBase
{
    private readonly IOasHierarchyService _service;
    public OasZonesController(IOasHierarchyService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasZoneDto>>> GetAll([FromQuery] Guid? siteId) => Ok(await _service.GetZonesAsync(CurrentTenantId, siteId));

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasZoneDto>> Create([FromBody] OasZoneRequestDto request) => Ok(await _service.CreateZoneAsync(CurrentTenantId, request));

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasZoneRequestDto request)
        => await _service.UpdateZoneAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpPost("{id}/archive")] [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> Archive(Guid id)
        => await _service.ArchiveZoneAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();
}
