using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.ShopFloorAuth.DTOs;
using MyApi.Modules.OAS.ShopFloorAuth.Services;

namespace MyApi.Modules.OAS.ShopFloorAuth.Controllers;

[ApiController]
[Route("api/oas/shopfloor")]
public class OasShopFloorAuthController : ControllerBase
{
    private readonly IOasShopFloorAuthService _service;

    public OasShopFloorAuthController(IOasShopFloorAuthService service) => _service = service;

    private string OasSlug => HttpContext.Items["OasSlug"] as string
        ?? throw new InvalidOperationException("OasSlug missing — OasTenantMiddleware did not run.");

    private int TenantId => HttpContext.Items.TryGetValue("TenantId", out var v) && v is int i ? i : 0;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<OasAuthResponseDto>> Login([FromBody] OasShopFloorLoginRequestDto request)
    {
        var result = await _service.LoginAsync(OasSlug, TenantId, request);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("pin/regenerate")]
    [Authorize(AuthenticationSchemes = OasAuthSchemes.SchemeName)]
    [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasPinRegenerateResponseDto>> RegeneratePin([FromQuery] Guid operatorId)
    {
        var actorRole = User.FindFirst("oas_role")?.Value ?? string.Empty;
        var result = await _service.RegeneratePinAsync(TenantId, operatorId, actorRole);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
