using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Hierarchy.DTOs;
using MyApi.Modules.OAS.Hierarchy.Services;

namespace MyApi.Modules.OAS.Hierarchy.Controllers;

[Route("api/oas/sites")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasSitesController : OasControllerBase
{
    private readonly IOasHierarchyService _service;
    public OasSitesController(IOasHierarchyService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasSiteDto>>> GetAll() => Ok(await _service.GetSitesAsync(CurrentTenantId));

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasSiteDto>> Create([FromBody] OasSiteRequestDto request) => Ok(await _service.CreateSiteAsync(CurrentTenantId, request));

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasSiteRequestDto request)
        => await _service.UpdateSiteAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpPost("{id}/archive")] [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> Archive(Guid id)
        => await _service.ArchiveSiteAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();
}
