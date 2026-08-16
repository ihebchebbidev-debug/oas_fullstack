using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Reporting.DTOs;
using MyApi.Modules.Reporting.Services;

namespace MyApi.Modules.Reporting.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportingFavoritesController : ControllerBase
{
    private readonly IReportingFavoritesService _service;
    private readonly ILogger<ReportingFavoritesController> _logger;

    // Allow-list of widget "sources". Keep in sync with the frontend
    // `FavoriteWidget.source` union in src/modules/reporting/store/useFavoritesStore.ts.
    private static readonly HashSet<string> AllowedSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sales", "Service", "Finance", "HR", "Purchase",
    };

    // Guard against a runaway client pushing an unbounded reorder payload
    // into the DB (each entry becomes a row update).
    private const int MaxReorderIds = 200;

    public ReportingFavoritesController(
        IReportingFavoritesService service,
        ILogger<ReportingFavoritesController> logger)
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
            _logger.LogError(ex, "ReportingFavorites GET failed");
            return StatusCode(500, new { success = false, message = "Failed to load favorites" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertReportingFavoriteRequest req, CancellationToken ct)
    {
        try
        {
            if (req == null || string.IsNullOrWhiteSpace(req.WidgetId))
                return BadRequest(new { success = false, message = "widgetId is required" });
            if (req.WidgetId.Length > 200)
                return BadRequest(new { success = false, message = "widgetId is too long" });
            if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 300)
                return BadRequest(new { success = false, message = "title must be 1..300 chars" });
            if (!AllowedSources.Contains(req.Source))
                return BadRequest(new { success = false, message = "source is invalid" });

            var data = await _service.UpsertAsync(GetCurrentUserId(), req, ct);
            return Ok(new { success = true, data });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReportingFavorites UPSERT failed");
            return StatusCode(500, new { success = false, message = "Failed to save favorite" });
        }
    }

    [HttpDelete("{widgetId}")]
    public async Task<IActionResult> Delete(string widgetId, [FromQuery] string? scope, CancellationToken ct)
    {
        try
        {
            var ok = await _service.DeleteAsync(GetCurrentUserId(), scope ?? "default", widgetId, ct);
            return Ok(new { success = ok });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReportingFavorites DELETE failed");
            return StatusCode(500, new { success = false, message = "Failed to remove favorite" });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAll([FromQuery] string? scope, CancellationToken ct)
    {
        try
        {
            var count = await _service.DeleteAllAsync(GetCurrentUserId(), scope ?? "default", ct);
            return Ok(new { success = true, removed = count });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReportingFavorites DELETE ALL failed");
            return StatusCode(500, new { success = false, message = "Failed to clear favorites" });
        }
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderReportingFavoritesRequest req, CancellationToken ct)
    {
        try
        {
            if (req == null) return BadRequest(new { success = false, message = "Body required" });
            if (req.OrderedWidgetIds == null)
                return BadRequest(new { success = false, message = "orderedWidgetIds is required" });
            if (req.OrderedWidgetIds.Count > MaxReorderIds)
                return BadRequest(new { success = false, message = "Too many items in reorder payload" });

            await _service.ReorderAsync(GetCurrentUserId(), req, ct);
            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReportingFavorites REORDER failed");
            return StatusCode(500, new { success = false, message = "Failed to reorder favorites" });
        }
    }
}
