using MyApi.Data;
using MyApi.Modules.Auth.DTOs;
using MyApi.Modules.Auth.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MyApi.Modules.Auth.Controllers
{
    /// <summary>
    /// Handles the second step of a 2FA login: verify the emailed OTP and (on success)
    /// finalize authentication by returning tokens. Also handles resending the OTP.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TwoFactorController : ControllerBase
    {
        private readonly ITwoFactorService _twoFactor;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IAuthService _auth;
        private readonly ILogger<TwoFactorController> _logger;

        public TwoFactorController(
            ITwoFactorService twoFactor,
            ApplicationDbContext context,
            IConfiguration configuration,
            IAuthService auth,
            ILogger<TwoFactorController> logger)
        {
            _twoFactor = twoFactor;
            _context = context;
            _configuration = configuration;
            _auth = auth;
            _logger = logger;
        }

        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] TwoFactorVerifyRequestDto dto)
        {
            var (ok, error, adminId, userId) = await _twoFactor.VerifyChallengeAsync(dto.ChallengeToken, dto.OtpCode);
            if (!ok)
            {
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = error ?? "invalid_code",
                });
            }

            if (adminId.HasValue)
            {
                var admin = await _context.MainAdminUsers.FirstOrDefaultAsync(a => a.Id == adminId.Value);
                if (admin == null) return Ok(new AuthResponseDto { Success = false, Message = "invalid_challenge" });
                var (access, refresh, expires) = IssueAdminTokens(admin);
                admin.LastLoginAt = DateTime.UtcNow;
                admin.LastLoginDate = DateTime.UtcNow;
                admin.AccessToken = access;
                admin.RefreshToken = refresh;
                admin.TokenExpiresAt = expires;
                admin.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var userDto = await _auth.GetUserByIdAsync(admin.Id);
                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Login successful",
                    AccessToken = access,
                    RefreshToken = refresh,
                    ExpiresAt = expires,
                    User = userDto,
                });
            }

            if (userId.HasValue)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
                if (user == null) return Ok(new AuthResponseDto { Success = false, Message = "invalid_challenge" });
                var (access, refresh, expires) = IssueUserTokens(user);
                user.LastLoginAt = DateTime.UtcNow;
                user.AccessToken = access;
                user.RefreshToken = refresh;
                user.TokenExpiresAt = expires;
                user.ModifiedDate = DateTime.UtcNow;
                user.ModifiedBy = user.Email;
                await _context.SaveChangesAsync();

                var userDto = await _auth.GetUserByIdAsync(user.Id);
                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Login successful",
                    AccessToken = access,
                    RefreshToken = refresh,
                    ExpiresAt = expires,
                    User = userDto,
                });
            }

            return Ok(new AuthResponseDto { Success = false, Message = "invalid_challenge" });
        }

        [HttpPost("resend")]
        public async Task<IActionResult> Resend([FromBody] TwoFactorResendRequestDto dto)
        {
            var (ok, error, cooldown) = await _twoFactor.ResendChallengeAsync(dto.ChallengeToken, dto.Language);
            return Ok(new
            {
                success = ok,
                message = ok ? "OTP resent" : (error ?? "error"),
                cooldownSeconds = cooldown,
            });
        }

        // ---- token issuance (mirrors AuthService) ----
        private (string, string, DateTime) IssueAdminTokens(Models.MainAdminUser admin)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
            var issuer = _configuration["Jwt:Issuer"] ?? "MyApi";
            var audience = _configuration["Jwt:Audience"] ?? "MyApiClients";
            var expiresAt = DateTime.UtcNow.AddHours(12);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new(ClaimTypes.Email, admin.Email),
                new(ClaimTypes.Name, admin.Username),
                new("login_type", "admin"),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(issuer, audience, claims, expires: expiresAt, signingCredentials: creds);
            var access = new JwtSecurityTokenHandler().WriteToken(token);
            var refresh = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            return (access, refresh, expiresAt);
        }

        private (string, string, DateTime) IssueUserTokens(Users.Models.User user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
            var issuer = _configuration["Jwt:Issuer"] ?? "MyApi";
            var audience = _configuration["Jwt:Audience"] ?? "MyApiClients";
            var expiresAt = DateTime.UtcNow.AddHours(12);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new("login_type", "user"),
                new(ClaimTypes.Role, user.Role ?? "User"),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(issuer, audience, claims, expires: expiresAt, signingCredentials: creds);
            var access = new JwtSecurityTokenHandler().WriteToken(token);
            var refresh = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            return (access, refresh, expiresAt);
        }
    }
}
