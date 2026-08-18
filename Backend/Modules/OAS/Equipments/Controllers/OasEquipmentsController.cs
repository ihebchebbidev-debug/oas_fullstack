using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Equipments.DTOs;
using MyApi.Modules.OAS.Equipments.Services;

namespace MyApi.Modules.OAS.Equipments.Controllers;

[Route("api/oas/equipments")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasEquipmentsController : OasControllerBase
{
    private readonly IOasEquipmentService _service;
    public OasEquipmentsController(IOasEquipmentService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasEquipmentDto>>> GetAll([FromQuery] Guid? postId) => Ok(await _service.GetAllAsync(CurrentTenantId, postId));

    // GetAll deliberately unrestricted — reloadAll() calls equipmentsApi.list() as part of the shared referentials load mobile's DeclareStop also triggers.
    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<ActionResult<OasEquipmentDto>> Create([FromBody] OasEquipmentRequestDto request) => Ok(await _service.CreateAsync(CurrentTenantId, request));

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasEquipmentRequestDto request)
        => await _service.UpdateAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpDelete("{id}")] [OasAuthorize(Roles = "admin")] [OasWorkspace("web")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();
}
