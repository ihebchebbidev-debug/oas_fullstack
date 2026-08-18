using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.ShopFloorAuth.DTOs;
using MyApi.Modules.OAS.ShopFloorAuth.Services;

namespace MyApi.Modules.OAS.ShopFloorAuth.Controllers;

/// <summary>Spec §6.1 "Opérateurs" — GET never includes pin (decision v12); the regenerate-pin route delegates to the same service as /shopfloor/pin/regenerate so the two documented routes never drift. Every action here is console-only (UsersPanel.tsx and the hidden SetupPage.tsx, both of which only ever hold a "web" workspace token) — no mobile screen manages other users.</summary>
[Route("api/oas/operators")]
[OasPluginGate("OA0011CONSOLE")]
[OasWorkspace("web")]
public class OasOperatorController : OasControllerBase
{
    private readonly IOasOperatorService _operators;
    private readonly IOasShopFloorAuthService _shopFloorAuth;

    public OasOperatorController(IOasOperatorService operators, IOasShopFloorAuthService shopFloorAuth)
    {
        _operators = operators;
        _shopFloorAuth = shopFloorAuth;
    }

    /// <summary>Directory read — every other action here is admin/supervisor-only (spec §8.0's console-role model); this GET carried no restriction at all and let any authenticated OAS user, including a plain shop-floor operator token, enumerate every user's email/role/scope in the tenant.</summary>
    [HttpGet]
    [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<IReadOnlyList<OasOperatorDto>>> Search([FromQuery] string? q, [FromQuery] Guid? scopeSiteId, [FromQuery] Guid? scopeZoneId, [FromQuery] Guid? scopeLineId)
    {
        var rows = await _operators.SearchAsync(CurrentTenantId, q, scopeSiteId, scopeZoneId, scopeLineId);
        return Ok(rows);
    }

    [HttpPost]
    [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasOperatorDto>> Create([FromBody] OasCreateOperatorRequestDto request)
    {
        var (success, error, dto) = await _operators.CreateAsync(CurrentTenantId, request, CurrentOasRole);
        if (!success)
        {
            return error == "admin_role_requires_admin_caller"
                ? Problem(statusCode: 403, title: error)
                : Problem(statusCode: 409, title: error ?? "conflict");
        }
        return Ok(dto);
    }

    [HttpPut("{id}/active")]
    [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] OasSetActiveRequestDto request)
    {
        var (success, error) = await _operators.SetActiveAsync(CurrentTenantId, id, request.IsActive, CurrentOasRole);
        if (!success)
        {
            return error switch
            {
                "not_found" => NotFound(),
                "admin_target_requires_admin_caller" => Problem(statusCode: 403, title: error),
                _ => BadRequest(new { error }),
            };
        }
        return Ok(new { success = true });
    }

    /// <summary>Relaxed from admin-only to admin,supervisor — the admin-target/admin-role restriction is enforced inside SetRoleAsync itself (mirrors Create), so a supervisor can promote/demote between operator/supervisor but still can't touch an admin either direction.</summary>
    [HttpPut("{id}/role")]
    [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> SetRole(Guid id, [FromBody] OasSetRoleRequestDto request)
    {
        var (success, error) = await _operators.SetRoleAsync(CurrentTenantId, id, request.Role, CurrentOasRole);
        if (!success)
        {
            return error switch
            {
                "not_found" => NotFound(),
                "admin_role_requires_admin_caller" => Problem(statusCode: 403, title: error),
                _ => BadRequest(new { error }),
            };
        }
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
