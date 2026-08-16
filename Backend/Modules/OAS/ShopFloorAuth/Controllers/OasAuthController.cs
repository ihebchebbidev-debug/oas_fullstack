using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.ShopFloorAuth.DTOs;
using MyApi.Modules.OAS.ShopFloorAuth.Services;

namespace MyApi.Modules.OAS.ShopFloorAuth.Controllers;

/// <summary>
/// Console auth (spec §8.2): POST /oas/setup, POST /api/oas/auth/*.
/// Shopfloor login (PIN/QR) is a separate controller (Lot 2) since it lives
/// under api/oas/shopfloor/* rather than api/oas/auth/*.
/// </summary>
[ApiController]
[Route("api/oas")]
public class OasAuthController : ControllerBase
{
    private readonly IOasAuthService _authService;

    public OasAuthController(IOasAuthService authService) => _authService = authService;

    private string OasSlug => HttpContext.Items["OasSlug"] as string
        ?? throw new InvalidOperationException("OasSlug missing — OasTenantMiddleware did not run.");

    private int TenantId => HttpContext.Items.TryGetValue("TenantId", out var v) && v is int i ? i : 0;

    /// <summary>Non-authenticated, one-time only. Refuses if an admin already exists (spec §8.2).</summary>
    [HttpPost("setup")]
    [AllowAnonymous]
    public async Task<ActionResult<OasAuthResponseDto>> Setup([FromBody] OasSetupRequestDto request)
    {
        var result = await _authService.SetupAsync(OasSlug, TenantId, request);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<ActionResult<OasAuthResponseDto>> Login([FromBody] OasLoginRequestDto request)
    {
        var result = await _authService.LoginAsync(OasSlug, TenantId, request);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpGet("auth/me")]
    [Authorize(AuthenticationSchemes = OasAuthSchemes.SchemeName)]
    public async Task<ActionResult<OasUserDto>> Me()
    {
        var idClaim = User.FindFirst("oas_user_id")?.Value;
        if (!Guid.TryParse(idClaim, out var id)) return Unauthorized();
        var user = await _authService.GetCurrentUserAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<OasAuthResponseDto>> Refresh([FromBody] OasRefreshRequestDto request)
    {
        var result = await _authService.RefreshAsync(request);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("auth/logout")]
    [Authorize(AuthenticationSchemes = OasAuthSchemes.SchemeName)]
    public async Task<IActionResult> Logout()
    {
        var idClaim = User.FindFirst("oas_user_id")?.Value;
        if (!Guid.TryParse(idClaim, out var id)) return Unauthorized();
        await _authService.LogoutAsync(id);
        return Ok(new { success = true });
    }

    [HttpPost("auth/change-password")]
    [Authorize(AuthenticationSchemes = OasAuthSchemes.SchemeName)]
    public async Task<ActionResult<OasAuthResponseDto>> ChangePassword([FromBody] OasChangePasswordRequestDto request)
    {
        var idClaim = User.FindFirst("oas_user_id")?.Value;
        if (!Guid.TryParse(idClaim, out var id)) return Unauthorized();
        var result = await _authService.ChangePasswordAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
