using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Quality.DTOs;
using MyApi.Modules.OAS.Quality.Services;

namespace MyApi.Modules.OAS.Quality.Controllers;

[Route("api/oas/quality-checks")]
[OasPluginGate("OA0004DECLARATIONS")]
public class OasQualityChecksController : OasControllerBase
{
    private readonly IOasQualityService _service;
    public OasQualityChecksController(IOasQualityService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<OasQualityCheckDto>> Create([FromBody] OasQualityCheckRequestDto request)
        => Ok(await _service.CreateCheckAsync(CurrentTenantId, CurrentOasUserId, request));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OasQualityCheckDto>>> GetAll([FromQuery] Guid? postId) => Ok(await _service.GetChecksAsync(CurrentTenantId, postId));
}

[Route("api/oas/quality-check-templates")]
[OasPluginGate("OA0004DECLARATIONS")]
public class OasQualityCheckTemplatesController : OasControllerBase
{
    private readonly IOasQualityService _service;
    public OasQualityCheckTemplatesController(IOasQualityService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasQualityCheckTemplateDto>>> GetAll() => Ok(await _service.GetTemplatesAsync(CurrentTenantId));

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasQualityCheckTemplateDto>> Create([FromBody] OasQualityCheckTemplateRequestDto request)
        => Ok(await _service.CreateTemplateAsync(CurrentTenantId, request));

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasQualityCheckTemplateRequestDto request)
        => await _service.UpdateTemplateAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpDelete("{id}")] [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteTemplateAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();

    [HttpGet("{id}/items")]
    public async Task<ActionResult<IReadOnlyList<OasQualityCheckTemplateItemDto>>> GetItems(Guid id) => Ok(await _service.GetTemplateItemsAsync(CurrentTenantId, id));

    [HttpPut("{id}/items")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> PutItems(Guid id, [FromBody] List<OasQualityCheckTemplateItemRequestDto> items)
    {
        await _service.PutTemplateItemsAsync(CurrentTenantId, id, items);
        return Ok(new { success = true });
    }
}
