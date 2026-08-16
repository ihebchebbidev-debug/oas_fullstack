using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Kpi.DTOs;
using MyApi.Modules.OAS.Kpi.Services;

namespace MyApi.Modules.OAS.Kpi.Controllers;

[Route("api/oas/kpi")]
[OasPluginGate("OA0002DASHBOARD")]
public class OasKpiController : OasControllerBase
{
    private readonly IOasKpiService _service;
    public OasKpiController(IOasKpiService service) => _service = service;

    private static (DateOnly from, DateOnly to) Range(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return (from ?? today, to ?? today);
    }

    [HttpGet("daily")]
    public async Task<ActionResult<OasKpiDailyDto>> Daily([FromQuery] Guid? postId, [FromQuery] Guid? lineId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t) = Range(from, to);
        return Ok(await _service.GetDailyAsync(CurrentTenantId, postId, lineId, f, t));
    }

    [HttpGet("pareto")]
    public async Task<ActionResult<IReadOnlyList<OasParetoEntryDto>>> Pareto([FromQuery] Guid? postId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t) = Range(from, to);
        return Ok(await _service.GetParetoAsync(CurrentTenantId, postId, f, t));
    }

    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<OasTrendPointDto>>> Trend([FromQuery] Guid? postId, [FromQuery] Guid? lineId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t) = Range(from, to);
        return Ok(await _service.GetTrendAsync(CurrentTenantId, postId, lineId, f, t));
    }

    [HttpGet("line-comparison")]
    public async Task<ActionResult<IReadOnlyList<OasLineComparisonEntryDto>>> LineComparison([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t) = Range(from, to);
        return Ok(await _service.GetLineComparisonAsync(CurrentTenantId, f, t));
    }

    [HttpGet("sla-summary")]
    public async Task<ActionResult<IReadOnlyList<OasSlaSummaryEntryDto>>> SlaSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t) = Range(from, to);
        return Ok(await _service.GetSlaSummaryAsync(CurrentTenantId, f, t));
    }

    [HttpGet("cadence-gap")]
    public async Task<ActionResult<IReadOnlyList<OasCadenceGapEntryDto>>> CadenceGap([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t) = Range(from, to);
        return Ok(await _service.GetCadenceGapAsync(CurrentTenantId, f, t));
    }
}

[Route("api/oas/andon/message")]
[OasPluginGate("OA0010ANDON")]
public class OasAndonMessageController : OasControllerBase
{
    private readonly IOasKpiService _service;
    public OasAndonMessageController(IOasKpiService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<OasAndonMessageDto>> Get([FromQuery] Guid? lineId)
    {
        var dto = await _service.GetAndonMessageAsync(CurrentTenantId, lineId);
        return Ok(dto ?? new OasAndonMessageDto { LineId = lineId, Message = "" });
    }

    [HttpPut] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasAndonMessageDto>> Set([FromBody] OasAndonMessageRequestDto request)
        => Ok(await _service.SetAndonMessageAsync(CurrentTenantId, CurrentOasUserId, request));
}
