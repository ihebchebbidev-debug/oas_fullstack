using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApi.Modules.Auth.Services;

namespace MyApi.Modules.Auth.Controllers
{
    /// <summary>
    /// Email verification endpoints.
    /// Requires a valid JWT (the user is already authenticated; we simply
    /// hold them on the verify page until EmailVerified flips to true).
    /// </summary>
    [ApiController]
    [Route("api/email-verification")]
    [Authorize]
    public class EmailVerificationController : ControllerBase
    {
        private readonly IEmailVerificationService _service;
        private readonly ILogger<EmailVerificationController> _logger;

        public EmailVerificationController(
            IEmailVerificationService service,
            ILogger<EmailVerificationController> logger)
        {
            _service = service;
            _logger = logger;
        }

        public class RequestCodeDto { public string? Lang { get; set; } }
        public class VerifyCodeDto  { public string Code { get; set; } = string.Empty; }

        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            var (type, userId) = ResolveCaller();
            if (userId <= 0) return Unauthorized();
            var result = await _service.GetStatusAsync(type, userId);
            return Ok(new
            {
                success = true,
                emailVerified = result.EmailVerified,
                email = MaskEmail(result.Email),
                canResendInSeconds = result.CanResendInSeconds,
            });
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestCode([FromBody] RequestCodeDto? dto)
        {
            var (type, userId) = ResolveCaller();
            if (userId <= 0) return Unauthorized();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var lang = dto?.Lang ?? "en";
            var result = await _service.RequestCodeAsync(type, userId, ip, lang);
            if (!result.Success)
            {
                return StatusCode(429, new
                {
                    success = false,
                    error = result.Message,
                    cooldownSeconds = result.CooldownSeconds,
                });
            }
            return Ok(new
            {
                success = true,
                cooldownSeconds = result.CooldownSeconds,
                expiresInSeconds = result.ExpiresInSeconds,
            });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] VerifyCodeDto dto)
        {
            var (type, userId) = ResolveCaller();
            if (userId <= 0) return Unauthorized();
            var result = await _service.VerifyCodeAsync(type, userId, dto?.Code ?? string.Empty);
            if (!result.Success)
                return BadRequest(new { success = false, error = result.ErrorCode });
            return Ok(new { success = true, emailVerified = true });
        }

        // ---- helpers ---------------------------------------------------
        private (EmailVerifyUserType type, int userId) ResolveCaller()
        {
            // Same claim shape used elsewhere in this project: "userId" + "userType" or role claim.
            var userIdStr = User.FindFirst("userId")?.Value
                            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out var userId)) return (EmailVerifyUserType.User, 0);

            var userTypeClaim = User.FindFirst("userType")?.Value
                                ?? User.FindFirst("login_type")?.Value
                                ?? User.FindFirst(ClaimTypes.Role)?.Value
                                ?? string.Empty;

            var isMain = userTypeClaim.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                         || userTypeClaim.Equals("MainAdmin", StringComparison.OrdinalIgnoreCase)
                         || userTypeClaim.Equals("admin", StringComparison.OrdinalIgnoreCase);

            // Convention used across this project: MainAdmin has id=1.
            if (userId == 1) isMain = true;

            return (isMain ? EmailVerifyUserType.MainAdmin : EmailVerifyUserType.User, userId);
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return email ?? string.Empty;
            var parts = email.Split('@', 2);
            var local = parts[0];
            var domain = parts[1];
            if (local.Length <= 2) return $"{local[0]}***@{domain}";
            return $"{local[0]}{new string('•', Math.Max(1, local.Length - 2))}{local[^1]}@{domain}";
        }
    }
}
