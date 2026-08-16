using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using MyApi.Modules.OfflineHydration.DTOs;
using MyApi.Modules.OfflineHydration.Services;
using System.Security.Claims;

namespace MyApi.Modules.OfflineHydration.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OfflineHydrationPreferencesController : ControllerBase
{
    private readonly IOfflineHydrationPreferencesService _service;

    public OfflineHydrationPreferencesController(IOfflineHydrationPreferencesService service)
    {
        _service = service;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("UserId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            throw new UnauthorizedAccessException("User ID not found in token");
        return userId;
    }

    /// <summary>
    /// Current user's offline hydration module toggles (per tenant).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var data = await _service.GetForUserAsync(userId, cancellationToken);
            return Ok(new { success = true, data });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Replace current user's module toggles. Only <c>false</c> values are persisted; omitted keys mean "enabled".
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> PutMine([FromBody] UpdateOfflineHydrationModulesRequest? body, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var modules = body?.Modules ?? new Dictionary<string, bool>();
            var data = await _service.UpsertForUserAsync(userId, modules, cancellationToken);
            return Ok(new { success = true, data });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
