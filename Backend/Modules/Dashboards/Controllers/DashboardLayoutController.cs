using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Dashboards.DTOs;
using MyApi.Modules.Dashboards.Services;

namespace MyApi.Modules.Dashboards.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardLayoutController : ControllerBase
{
    private readonly IDashboardLayoutService _service;
    private readonly ILogger<DashboardLayoutController> _logger;

    // Hard caps so a buggy/malicious client can't push unbounded arrays into
    // the jsonb columns. The main dashboard has < 20 cards in practice.
    private const int MaxIds = 200;
    private const int MaxIdLength = 200;

    public DashboardLayoutController(
        IDashboardLayoutService service,
        ILogger<DashboardLayoutController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst("UserId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("User ID not found in token");
        return id;
    }

    private static bool AnyOversized(IEnumerable<string>? list) =>
        list != null && list.Any(s => s == null || s.Length > MaxIdLength);

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? scope, CancellationToken ct)
    {
        try
        {
            var data = await _service.GetAsync(GetCurrentUserId(), scope ?? "default", ct);
            return Ok(new { success = true, data });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DashboardLayout GET failed");
            return StatusCode(500, new { success = false, message = "Failed to load layout" });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] SaveDashboardLayoutRequest req, CancellationToken ct)
    {
        try
        {
            if (req == null) return BadRequest(new { success = false, message = "Body required" });
            var order = req.Order ?? new List<string>();
            var hidden = req.Hidden ?? new List<string>();
            if (order.Count > MaxIds || hidden.Count > MaxIds)
                return BadRequest(new { success = false, message = "Too many items in layout" });
            if (AnyOversized(order) || AnyOversized(hidden))
                return BadRequest(new { success = false, message = "One or more ids are too long" });

            var data = await _service.SaveAsync(GetCurrentUserId(), req, ct);
            return Ok(new { success = true, data });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DashboardLayout SAVE failed");
            return StatusCode(500, new { success = false, message = "Failed to save layout" });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Reset([FromQuery] string? scope, CancellationToken ct)
    {
        try
        {
            var ok = await _service.ResetAsync(GetCurrentUserId(), scope ?? "default", ct);
            return Ok(new { success = ok });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DashboardLayout RESET failed");
            return StatusCode(500, new { success = false, message = "Failed to reset layout" });
        }
    }
}
