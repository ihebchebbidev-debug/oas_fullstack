using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Lookups.DTOs;
using MyApi.Modules.OAS.Lookups.Services;

namespace MyApi.Modules.OAS.Lookups.Controllers;

[Route("api/oas/lookups")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasLookupsController : OasControllerBase
{
    private readonly IOasLookupService _service;
    public OasLookupsController(IOasLookupService service) => _service = service;

    [HttpGet("{type}")]
    public async Task<ActionResult<IReadOnlyList<OasLookupValueDto>>> GetByType(string type) => Ok(await _service.GetByTypeAsync(CurrentTenantId, type));

    [HttpPost("{type}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasLookupValueDto>> Create(string type, [FromBody] OasLookupValueRequestDto request)
        => Ok(await _service.CreateAsync(CurrentTenantId, type, request));

    [HttpPut("{type}/{id}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Update(string type, Guid id, [FromBody] OasLookupValueRequestDto request)
        => await _service.UpdateAsync(CurrentTenantId, type, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpDelete("{type}/{id}")] [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> Delete(string type, Guid id)
        => await _service.DeleteAsync(CurrentTenantId, type, id) ? Ok(new { success = true }) : NotFound();
}
