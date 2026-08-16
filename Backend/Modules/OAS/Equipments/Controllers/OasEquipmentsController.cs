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

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasEquipmentDto>> Create([FromBody] OasEquipmentRequestDto request) => Ok(await _service.CreateAsync(CurrentTenantId, request));

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasEquipmentRequestDto request)
        => await _service.UpdateAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpDelete("{id}")] [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();
}
