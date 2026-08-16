using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.ShopFloorAuth.DTOs;
using MyApi.Modules.OAS.ShopFloorAuth.Services;

namespace MyApi.Modules.OAS.ShopFloorAuth.Controllers;

/// <summary>Spec §6.1 "Opérateurs" — GET never includes pin (decision v12); the regenerate-pin route delegates to the same service as /shopfloor/pin/regenerate so the two documented routes never drift.</summary>
[Route("api/oas/operators")]
[OasPluginGate("OA0011CONSOLE")]
public class OasOperatorController : OasControllerBase
{
    private readonly IOasOperatorService _operators;
    private readonly IOasShopFloorAuthService _shopFloorAuth;

    public OasOperatorController(IOasOperatorService operators, IOasShopFloorAuthService shopFloorAuth)
    {
        _operators = operators;
        _shopFloorAuth = shopFloorAuth;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OasOperatorDto>>> Search([FromQuery] string? q, [FromQuery] Guid? scopeSiteId, [FromQuery] Guid? scopeZoneId, [FromQuery] Guid? scopeLineId)
    {
        var rows = await _operators.SearchAsync(CurrentTenantId, q, scopeSiteId, scopeZoneId, scopeLineId);
        return Ok(rows);
    }

    [HttpPost]
    [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasOperatorDto>> Create([FromBody] OasCreateOperatorRequestDto request)
    {
        var (success, error, dto) = await _operators.CreateAsync(CurrentTenantId, request);
        if (!success) return Problem(statusCode: 409, title: error ?? "conflict");
        return Ok(dto);
    }

    [HttpPut("{id}/active")]
    [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] OasSetActiveRequestDto request)
    {
        var ok = await _operators.SetActiveAsync(CurrentTenantId, id, request.IsActive);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpPut("{id}/role")]
    [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> SetRole(Guid id, [FromBody] OasSetRoleRequestDto request)
    {
        var (success, error) = await _operators.SetRoleAsync(CurrentTenantId, id, request.Role);
        if (!success) return error == "not_found" ? NotFound() : BadRequest(new { error });
        return Ok(new { success = true });
    }

    [HttpPut("{id}/scope")]
    [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> SetScope(Guid id, [FromBody] OasSetScopeRequestDto request)
    {
        var ok = await _operators.SetScopeAsync(CurrentTenantId, id, request);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpPost("{id}/regenerate-pin")]
    [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasPinRegenerateResponseDto>> RegeneratePin(Guid id)
    {
        var result = await _shopFloorAuth.RegeneratePinAsync(CurrentTenantId, id, CurrentOasRole);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
