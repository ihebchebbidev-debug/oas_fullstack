using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.Shared.Services;

namespace MyApi.Modules.Auth.Services
{
    public enum EmailVerifyUserType { MainAdmin, User }

    public class RequestCodeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CooldownSeconds { get; set; }
        public int ExpiresInSeconds { get; set; }
    }

    public class VerifyCodeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty; // invalid_code | expired | too_many_attempts | rate_limited
        public bool EmailVerified { get; set; }
    }

    public class StatusResult
    {
        public bool EmailVerified { get; set; }
        public string Email { get; set; } = string.Empty;
        public int CanResendInSeconds { get; set; }
    }

    public interface IEmailVerificationService
    {
        Task<StatusResult> GetStatusAsync(EmailVerifyUserType type, int userId);
        Task<RequestCodeResult> RequestCodeAsync(EmailVerifyUserType type, int userId, string ip, string lang);
        Task<VerifyCodeResult> VerifyCodeAsync(EmailVerifyUserType type, int userId, string code);
    }

    /// <summary>
    /// Email verification via 6-digit OTP.
    /// Reuses ForgotEmailService.SendOtpEmailAsync (same SMTP + i18n).
    /// Codes are SHA-256 hashed at rest, 10-minute expiry, 5 attempts max,
    /// 60s resend cooldown, 3 codes / 15 min per user, 10 codes / hour per IP.
    /// </summary>
    public class EmailVerificationService : IEmailVerificationService
    {
        private const int CodeTtlMinutes = 10;
        private const int ResendCooldownSeconds = 60;
        private const int MaxAttempts = 5;
        private const int UserWindowMinutes = 15;
        private const int UserWindowMax = 3;
        private const int IpWindowMinutes = 60;
        private const int IpWindowMax = 10;

        private readonly ApplicationDbContext _db;
        private readonly IForgotEmailService _mail;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EmailVerificationService> _logger;

        public EmailVerificationService(
            ApplicationDbContext db,
            IForgotEmailService mail,
            IMemoryCache cache,
            ILogger<EmailVerificationService> logger)
        {
            _db = db;
            _mail = mail;
            _cache = cache;
            _logger = logger;
        }

        public async Task<StatusResult> GetStatusAsync(EmailVerifyUserType type, int userId)
        {
            if (type == EmailVerifyUserType.MainAdmin)
            {
                var a = await _db.MainAdminUsers.FindAsync(userId);
                if (a == null) return new StatusResult();
                return new StatusResult
                {
                    EmailVerified = a.EmailVerified,
                    Email = a.Email ?? string.Empty,
                    CanResendInSeconds = ComputeCooldown(a.EmailVerifyOtpLastSentAt),
                };
            }
            var u = await _db.Users.FindAsync(userId);
            if (u == null) return new StatusResult();
            return new StatusResult
            {
                EmailVerified = u.EmailVerified,
                Email = u.Email ?? string.Empty,
                CanResendInSeconds = ComputeCooldown(u.EmailVerifyOtpLastSentAt),
            };
        }

        public async Task<RequestCodeResult> RequestCodeAsync(EmailVerifyUserType type, int userId, string ip, string lang)
        {
            var normalizedLang = string.Equals(lang, "fr", StringComparison.OrdinalIgnoreCase) ? "fr" : "en";

            string email;
            string firstName;
            DateTime? lastSent;
            bool alreadyVerified;

            if (type == EmailVerifyUserType.MainAdmin)
            {
                var a = await _db.MainAdminUsers.FindAsync(userId);
                if (a == null) return new RequestCodeResult { Success = false, Message = "User not found" };
                email = a.Email;
                firstName = a.FirstName ?? "User";
                lastSent = a.EmailVerifyOtpLastSentAt;
                alreadyVerified = a.EmailVerified;
            }
            else
            {
                var u = await _db.Users.FindAsync(userId);
                if (u == null) return new RequestCodeResult { Success = false, Message = "User not found" };
                email = u.Email;
                firstName = u.FirstName ?? "User";
                lastSent = u.EmailVerifyOtpLastSentAt;
                alreadyVerified = u.EmailVerified;
            }

            if (alreadyVerified)
                return new RequestCodeResult { Success = true, Message = "already_verified" };

            // Per-user cooldown
            var cooldown = ComputeCooldown(lastSent);
            if (cooldown > 0)
            {
                return new RequestCodeResult
                {
                    Success = false,
                    Message = "rate_limited",
                    CooldownSeconds = cooldown,
                };
            }

            // Per-user window
            var userKey = $"emailverify:user:{(int)type}:{userId}";
            if (!TryConsume(userKey, UserWindowMax, TimeSpan.FromMinutes(UserWindowMinutes)))
                return new RequestCodeResult { Success = false, Message = "rate_limited", CooldownSeconds = ResendCooldownSeconds };

            // Per-IP window
            var ipKey = $"emailverify:ip:{ip}";
            if (!TryConsume(ipKey, IpWindowMax, TimeSpan.FromMinutes(IpWindowMinutes)))
                return new RequestCodeResult { Success = false, Message = "rate_limited", CooldownSeconds = ResendCooldownSeconds };

            // Generate 6-digit code
            var code = GenerateNumericCode(6);
            var hash = Sha256(code);
            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(CodeTtlMinutes);

            if (type == EmailVerifyUserType.MainAdmin)
            {
                var a = await _db.MainAdminUsers.FindAsync(userId);
                a!.EmailVerifyOtpHash = hash;
                a.EmailVerifyOtpExpiresAt = expires;
                a.EmailVerifyOtpAttempts = 0;
                a.EmailVerifyOtpLastSentAt = now;
            }
            else
            {
                var u = await _db.Users.FindAsync(userId);
                u!.EmailVerifyOtpHash = hash;
                u.EmailVerifyOtpExpiresAt = expires;
                u.EmailVerifyOtpAttempts = 0;
                u.EmailVerifyOtpLastSentAt = now;
            }
            await _db.SaveChangesAsync();

            // Dedicated verification-email template (not password reset copy).
            var sent = await _mail.SendEmailVerificationAsync(email, code, firstName, normalizedLang, CodeTtlMinutes);
            if (!sent)
            {
                _logger.LogWarning("Failed to send verification email to {Email}", email);
                return new RequestCodeResult { Success = false, Message = "send_failed" };
            }

            return new RequestCodeResult
            {
                Success = true,
                CooldownSeconds = ResendCooldownSeconds,
                ExpiresInSeconds = CodeTtlMinutes * 60,
            };
        }

        public async Task<VerifyCodeResult> VerifyCodeAsync(EmailVerifyUserType type, int userId, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length < 4)
                return new VerifyCodeResult { Success = false, ErrorCode = "invalid_code" };

            code = code.Trim();

            if (type == EmailVerifyUserType.MainAdmin)
            {
                var a = await _db.MainAdminUsers.FindAsync(userId);
                if (a == null) return new VerifyCodeResult { Success = false, ErrorCode = "invalid_code" };
                if (a.EmailVerified) return new VerifyCodeResult { Success = true, EmailVerified = true };
                if (a.EmailVerifyOtpExpiresAt == null || a.EmailVerifyOtpExpiresAt < DateTime.UtcNow)
                    return new VerifyCodeResult { Success = false, ErrorCode = "expired" };
                if (a.EmailVerifyOtpAttempts >= MaxAttempts)
                    return new VerifyCodeResult { Success = false, ErrorCode = "too_many_attempts" };
                a.EmailVerifyOtpAttempts += 1;
                if (!ConstantTimeEquals(a.EmailVerifyOtpHash ?? "", Sha256(code)))
                {
                    await _db.SaveChangesAsync();
                    return new VerifyCodeResult { Success = false, ErrorCode = "invalid_code" };
                }
                a.EmailVerified = true;
                a.EmailVerifiedAt = DateTime.UtcNow;
                a.EmailVerifyOtpHash = null;
                a.EmailVerifyOtpExpiresAt = null;
                await _db.SaveChangesAsync();
                return new VerifyCodeResult { Success = true, EmailVerified = true };
            }
            else
            {
                var u = await _db.Users.FindAsync(userId);
                if (u == null) return new VerifyCodeResult { Success = false, ErrorCode = "invalid_code" };
                if (u.EmailVerified) return new VerifyCodeResult { Success = true, EmailVerified = true };
                if (u.EmailVerifyOtpExpiresAt == null || u.EmailVerifyOtpExpiresAt < DateTime.UtcNow)
                    return new VerifyCodeResult { Success = false, ErrorCode = "expired" };
                if (u.EmailVerifyOtpAttempts >= MaxAttempts)
                    return new VerifyCodeResult { Success = false, ErrorCode = "too_many_attempts" };
                u.EmailVerifyOtpAttempts += 1;
                if (!ConstantTimeEquals(u.EmailVerifyOtpHash ?? "", Sha256(code)))
                {
                    await _db.SaveChangesAsync();
                    return new VerifyCodeResult { Success = false, ErrorCode = "invalid_code" };
                }
                u.EmailVerified = true;
                u.EmailVerifiedAt = DateTime.UtcNow;
                u.EmailVerifyOtpHash = null;
                u.EmailVerifyOtpExpiresAt = null;
                await _db.SaveChangesAsync();
                return new VerifyCodeResult { Success = true, EmailVerified = true };
            }
        }

        // ---- helpers ---------------------------------------------------
        private static int ComputeCooldown(DateTime? lastSent)
        {
            if (lastSent == null) return 0;
            var since = (DateTime.UtcNow - lastSent.Value).TotalSeconds;
            var remaining = ResendCooldownSeconds - (int)since;
            return remaining > 0 ? remaining : 0;
        }

        private bool TryConsume(string key, int max, TimeSpan window)
        {
            var count = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = window;
                entry.Size = 1; // required: shared MemoryCache has SizeLimit configured
                return 0;
            });
            if (count >= max) return false;
            _cache.Set(key, count + 1, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = window,
                Size = 1
            });
            return true;
        }

        private static string GenerateNumericCode(int digits)
        {
            var buf = new byte[4];
            RandomNumberGenerator.Fill(buf);
            var n = BitConverter.ToUInt32(buf, 0);
            var mod = (int)Math.Pow(10, digits);
            return (n % mod).ToString().PadLeft(digits, '0');
        }

        private static string Sha256(string s)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
