using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Infrastructure;
using MyApi.Modules.ModuleRequests.DTOs;
using MyApi.Modules.ModuleRequests.Services;

namespace MyApi.Modules.ModuleRequests.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/module-requests")]
    public class ModuleRequestsController : ControllerBase
    {
        private readonly IModuleRequestEmailService _email;
        private readonly ILogger<ModuleRequestsController> _logger;

        public ModuleRequestsController(
            IModuleRequestEmailService email,
            ILogger<ModuleRequestsController> logger)
        {
            _email = email;
            _logger = logger;
        }

        private string? TenantFromHeader() =>
            Request.Headers.TryGetValue(TenantMiddleware.TenantHeaderName, out var t) && !string.IsNullOrWhiteSpace(t)
                ? t.ToString()
                : null;

        /// <summary>
        /// POST /api/module-requests — email a module activation/deactivation
        /// request to the Flowentra contact inbox.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ModuleRequestResultDto>> Create([FromBody] ModuleRequestDto dto)
        {
            if (dto == null) return BadRequest(new { success = false, error = "Body is required" });

            var action = (dto.Action ?? "").Trim().ToLowerInvariant();
            if (action != "activate" && action != "deactivate")
                return BadRequest(new { success = false, error = "Action must be 'activate' or 'deactivate'" });

            if (string.IsNullOrWhiteSpace(dto.ModuleCode) && string.IsNullOrWhiteSpace(dto.ModuleKey))
                return BadRequest(new { success = false, error = "ModuleCode or ModuleKey is required" });

            dto.Action = action;
            dto.Reason = dto.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length < 10)
                return BadRequest(new { success = false, error = "A message of at least 10 characters is required" });
            if (dto.Reason is { Length: > 2000 }) dto.Reason = dto.Reason.Substring(0, 2000);

            // Trust the server-side identity over client-provided values.
            var claimEmail = User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirst("email")?.Value;
            if (!string.IsNullOrWhiteSpace(claimEmail)) dto.UserEmail = claimEmail;
            if (string.IsNullOrWhiteSpace(dto.UserName))
                dto.UserName = User?.Identity?.Name ?? User?.FindFirstValue(ClaimTypes.Name);

            dto.TenantSlug = TenantFromHeader() ?? dto.TenantSlug;

            var sent = await _email.SendModuleRequestAsync(dto);
            if (!sent)
            {
                return StatusCode(500, new ModuleRequestResultDto
                {
                    Success = false,
                    Error = "Failed to send the request email. Please try again later.",
                    RequestedAtUtc = DateTime.UtcNow,
                });
            }

            return Ok(new ModuleRequestResultDto
            {
                Success = true,
                SentTo = _email.RecipientAddress,
                RequestedAtUtc = DateTime.UtcNow,
            });
        }
    }
}