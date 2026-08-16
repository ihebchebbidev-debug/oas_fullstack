using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Imports.DTOs;
using MyApi.Modules.OAS.Imports.Services;

namespace MyApi.Modules.OAS.Imports.Controllers;

[Route("api/oas/imports")]
[OasPluginGate("OA0008REFERENTIALS")]
[OasAuthorize(Roles = "admin,supervisor")]
public class OasImportsController : OasControllerBase
{
    private readonly IOasImportService _service;
    public OasImportsController(IOasImportService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasImportDto>>> GetAll() => Ok(await _service.GetAllAsync(CurrentTenantId));

    [HttpGet("{id}")]
    public async Task<ActionResult<OasImportDto>> GetOne(Guid id)
    {
        var dto = await _service.GetOneAsync(CurrentTenantId, id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("{id}/lines")]
    public async Task<ActionResult<IReadOnlyList<OasImportLineDto>>> GetLines(Guid id) => Ok(await _service.GetLinesAsync(CurrentTenantId, id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OasImportCreateRequestDto request)
    {
        var (success, error, dto) = await _service.CreateAsync(CurrentTenantId, CurrentOasUserId, request);
        return success ? Ok(dto) : BadRequest(new { error });
    }

    [HttpPost("{id}/commit")]
    public async Task<IActionResult> Commit(Guid id)
    {
        var (success, error, dto) = await _service.CommitAsync(CurrentTenantId, id);
        if (!success) return error == "not_found" ? NotFound() : BadRequest(new { error });
        return Ok(dto);
    }
}
