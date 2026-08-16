using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApi.Modules.PlanningProfiles.DTOs;
using MyApi.Modules.PlanningProfiles.Services;
using MyApi.Modules.Shared.Services;

namespace MyApi.Modules.PlanningProfiles.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/planning-profiles")]
    public class PlanningProfilesController : ControllerBase
    {
        private const string ModuleName = "PlanningProfiles";

        private readonly IPlanningProfileService _svc;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<PlanningProfilesController> _logger;

        public PlanningProfilesController(
            IPlanningProfileService svc,
            ISystemLogService systemLogService,
            ILogger<PlanningProfilesController> logger)
        {
            _svc = svc;
            _systemLogService = systemLogService;
            _logger = logger;
        }

        private string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "0";

        private string CurrentUserName =>
            User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? CurrentUserId;

        [HttpGet]
        public async Task<IActionResult> List()
        {
            try { return Ok(await _svc.ListAsync(CurrentUserId)); }
            catch (Exception ex) { return Fail(ex, "list"); }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            try
            {
                var p = await _svc.GetActiveAsync(CurrentUserId);
                return p == null ? NotFound() : Ok(p);
            }
            catch (Exception ex) { return Fail(ex, "read"); }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                var p = await _svc.GetByIdAsync(id, CurrentUserId);
                return p == null ? NotFound() : Ok(p);
            }
            catch (Exception ex) { return Fail(ex, "read", id.ToString()); }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePlanningProfileDto dto)
        {
            try
            {
                var result = await _svc.CreateAsync(dto, CurrentUserId);
                await _systemLogService.LogSuccessAsync(
                    $"Planning profile '{result.Name}' created",
                    ModuleName, "create", CurrentUserId, CurrentUserName,
                    "PlanningProfile", result.Id.ToString(),
                    $"isShared={result.IsShared}");
                return Ok(result);
            }
            catch (Exception ex) { return Fail(ex, "create"); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePlanningProfileDto dto)
        {
            try
            {
                var result = await _svc.UpdateAsync(id, dto, CurrentUserId);
                await _systemLogService.LogSuccessAsync(
                    $"Planning profile '{result.Name}' updated",
                    ModuleName, "update", CurrentUserId, CurrentUserName,
                    "PlanningProfile", result.Id.ToString(),
                    $"isShared={result.IsShared}");
                return Ok(result);
            }
            catch (Exception ex) { return Fail(ex, "update", id.ToString()); }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _svc.DeleteAsync(id, CurrentUserId);
                await _systemLogService.LogSuccessAsync(
                    $"Planning profile {id} deleted",
                    ModuleName, "delete", CurrentUserId, CurrentUserName,
                    "PlanningProfile", id.ToString());
                return NoContent();
            }
            catch (Exception ex) { return Fail(ex, "delete", id.ToString()); }
        }

        [HttpPut("active/{id:int}")]
        public async Task<IActionResult> SetActive(int id)
        {
            try
            {
                await _svc.SetActiveAsync(id, CurrentUserId);
                await _systemLogService.LogSuccessAsync(
                    $"Planning profile {id} set active",
                    ModuleName, "update", CurrentUserId, CurrentUserName,
                    "PlanningProfile", id.ToString());
                return NoContent();
            }
            catch (Exception ex) { return Fail(ex, "set_active", id.ToString()); }
        }

        // Central exception → HTTP status mapping so the controller stays flat.
        private IActionResult Fail(Exception ex, string action, string? entityId = null)
        {
            // Fire-and-forget best-effort audit; never let logging block the response path.
            try
            {
                _ = _systemLogService.LogWarningAsync(
                    $"PlanningProfiles.{action} failed: {ex.Message}",
                    ModuleName, action, CurrentUserId, CurrentUserName,
                    "PlanningProfile", entityId, ex.GetType().Name);
            }
            catch { /* logging must not throw */ }

            switch (ex)
            {
                case UnauthorizedAccessException:
                    return StatusCode(403, new { error = new { code = "FORBIDDEN", message = ex.Message } });
                case InvalidOperationException when ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase):
                    return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
                case ArgumentException:
                    return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = ex.Message } });
                default:
                    _logger.LogError(ex, "Unhandled PlanningProfiles.{Action} error", action);
                    return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }
    }
}
