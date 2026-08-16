using System;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApi.Modules.Sync.DTOs;
using MyApi.Modules.Sync.Services;

namespace MyApi.Modules.Sync.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SyncController : ControllerBase
    {
        private readonly ISyncService _syncService;
        private readonly ILogger<SyncController> _logger;

        public SyncController(ISyncService syncService, ILogger<SyncController> logger)
        {
            _syncService = syncService;
            _logger = logger;
        }

        [HttpPost("push")]
        public async Task<ActionResult<SyncPushResponseDto>> Push([FromBody] SyncPushRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (request.Operations == null || request.Operations.Count == 0) return Ok(new SyncPushResponseDto());
            
            // ✅ Read tenant from X-Tenant header with fallback to JWT claims or "default"
            var tenant = HttpContext.Request.Headers["X-Tenant"].ToString()?.Trim();
            
            // Fallback: Try to get tenant from JWT claims if not in header
            if (string.IsNullOrWhiteSpace(tenant))
            {
                tenant = User.Claims.FirstOrDefault(c => c.Type == "tenant")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(tenant))
                {
                    _logger.LogInformation("Sync push: Using tenant from JWT claims: {Tenant}", tenant);
                }
            }
            
            // Last resort: Use default tenant for backward compatibility
            if (string.IsNullOrWhiteSpace(tenant))
            {
                tenant = "default";
                _logger.LogInformation("Sync push: Using default tenant (no X-Tenant header or JWT claim)");
            }
            
            var currentUser = GetCurrentUser();
            
            // ✅ SOLUTION 2: Better error handling for missing user identity
            if (string.IsNullOrWhiteSpace(currentUser))
            {
                _logger.LogError("Sync push failed: Unable to extract user identity from JWT claims. Tenant={Tenant}", tenant);
                _logger.LogDebug("Available JWT claims: {Claims}", 
                    string.Join("; ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
                return Unauthorized(new { 
                    status = 401,
                    error = "Unauthorized",
                    message = "Unable to resolve user identity. Your authentication token may be invalid or expired."
                });
            }
            
            _logger.LogInformation("Sync push received: User={User}, Tenant={Tenant}, Operations={Count}",
                currentUser, tenant, request.Operations.Count);
            
            var result = await _syncService.PushAsync(request, currentUser, tenant);
            return Ok(result);
        }

        [HttpGet("pull")]
        public async Task<ActionResult<SyncPullResponseDto>> Pull([FromQuery] string? cursor = null, [FromQuery] int limit = 200)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (currentUser == null) return Unauthorized("User identity claim is required");
                var result = await _syncService.PullAsync(cursor, limit, currentUser, IsAdmin());
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("history")]
        public async Task<ActionResult<SyncHistoryResponseDto>> History([FromQuery] SyncHistoryQueryDto query)
        {
            var currentUser = GetCurrentUser();
            if (currentUser == null) return Unauthorized("User identity claim is required");
            var result = await _syncService.GetHistoryAsync(query, currentUser, IsAdmin());
            return Ok(result);
        }

        [HttpPost("retry")]
        public async Task<ActionResult<SyncPushResultDto>> Retry([FromBody] SyncRetryRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.OpId))
                return BadRequest("deviceId and opId are required");
            try
            {
                var currentUser = GetCurrentUser();
                if (currentUser == null) return Unauthorized("User identity claim is required");
                var result = await _syncService.RetryAsync(request, currentUser, IsAdmin());
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private string? GetCurrentUser()
        {
            // ✅ SOLUTION 2: Try multiple claim types for robustness
            // Standard email claim (most common)
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrWhiteSpace(email))
                return email;
            
            // Custom email claim (camelCase)
            email = User.FindFirst("email")?.Value;
            if (!string.IsNullOrWhiteSpace(email))
                return email;
            
            // Custom email claim (PascalCase)
            email = User.FindFirst("Email")?.Value;
            if (!string.IsNullOrWhiteSpace(email))
                return email;
            
            // Fallback: If no email claim, try subject identifier
            // (less ideal but better than null)
            var subject = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(subject))
            {
                _logger.LogWarning("Using NameIdentifier claim as user ID instead of email: {Subject}", subject);
                return subject;
            }
            
            // Azure AD Object ID fallback
            subject = User.FindFirst("oid")?.Value;
            if (!string.IsNullOrWhiteSpace(subject))
            {
                _logger.LogWarning("Using Azure OID claim as user ID instead of email: {Subject}", subject);
                return subject;
            }
            
            // Last resort: JWT sub claim
            subject = User.FindFirst("sub")?.Value;
            if (!string.IsNullOrWhiteSpace(subject))
            {
                _logger.LogWarning("Using sub claim as user ID instead of email: {Subject}", subject);
                return subject;
            }
            
            return null;
        }

        private bool IsAdmin()
        {
            return User.IsInRole("admin")
                || User.IsInRole("Admin")
                || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(User.FindFirst("role")?.Value, "admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
