using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Shifts.DTOs;
using MyApi.Modules.OAS.Shifts.Services;

namespace MyApi.Modules.OAS.Shifts.Controllers;

[Route("api/oas/shifts")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasShiftsController : OasControllerBase
{
    private readonly IOasShiftService _service;
    public OasShiftsController(IOasShiftService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasShiftTemplateDto>>> GetAll() => Ok(await _service.GetTemplatesAsync(CurrentTenantId));

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Create([FromBody] OasShiftTemplateRequestDto request)
    {
        var (success, error, dto) = await _service.CreateTemplateAsync(CurrentTenantId, request);
        return success ? Ok(dto) : BadRequest(new { error });
    }

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasShiftTemplateRequestDto request)
    {
        var (success, error) = await _service.UpdateTemplateAsync(CurrentTenantId, id, request);
        if (!success) return error == "not_found" ? NotFound() : BadRequest(new { error });
        return Ok(new { success = true });
    }

    [HttpDelete("{id}")] [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteTemplateAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();

    [HttpGet("calendar")]
    public async Task<ActionResult<IReadOnlyList<OasShiftCalendarEntryDto>>> GetCalendar([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(await _service.GetCalendarAsync(CurrentTenantId, from, to));

    [HttpPut("calendar")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> PutCalendar([FromQuery] Guid siteId, [FromBody] List<OasShiftCalendarEntryDto> entries)
    {
        await _service.PutCalendarAsync(CurrentTenantId, siteId, entries);
        return Ok(new { success = true });
    }
}

[Route("api/oas/shift-signoffs")]
[OasPluginGate("OA0009REPORTING")]
public class OasShiftSignoffsController : OasControllerBase
{
    private readonly IOasShiftService _service;
    public OasShiftSignoffsController(IOasShiftService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<OasShiftSignoffDto>> Create([FromBody] OasShiftSignoffRequestDto request)
        => Ok(await _service.CreateSignoffAsync(CurrentTenantId, CurrentOasUserId, request));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OasShiftSignoffDto>>> GetAll([FromQuery] Guid? shift, [FromQuery] DateOnly? date)
        => Ok(await _service.GetSignoffsAsync(CurrentTenantId, shift, date));
}
