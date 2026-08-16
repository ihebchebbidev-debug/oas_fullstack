using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Audit.DTOs;
using MyApi.Modules.OAS.Audit.Services;
using MyApi.Modules.OAS.Common;

namespace MyApi.Modules.OAS.Audit.Controllers;

[Route("api/oas/audit")]
[OasPluginGate("OA0011CONSOLE")]
[OasAuthorize(Roles = "admin,supervisor")]
public class OasAuditController : OasControllerBase
{
    private readonly IOasAuditService _service;
    public OasAuditController(IOasAuditService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OasAuditLogDto>>> GetAll(
        [FromQuery] string? entity, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] string? actor, [FromQuery] string? action, [FromQuery] string? q)
        => Ok(await _service.GetAsync(CurrentTenantId, entity, from, to, actor, action, q));
}
