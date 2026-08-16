using MyApi.Data;
using MyApi.Modules.Auth.Models;
using MyApi.Modules.Users.Models;
using MyApi.Modules.Shared.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace MyApi.Modules.Auth.Services
{
    public interface ITwoFactorService
    {
        Task<(string ChallengeToken, string MaskedEmail)> IssueAdminChallengeAsync(MainAdminUser admin, string language = "en");
        Task<(string ChallengeToken, string MaskedEmail)> IssueUserChallengeAsync(User user, string language = "en");

        /// <summary>Verify OTP for a pending challenge. Returns (adminId?, userId?) on success.</summary>
        Task<(bool Ok, string? Error, int? AdminId, int? UserId)> VerifyChallengeAsync(string challengeToken, string otpCode);

        /// <summary>Resend the OTP for a pending challenge (rate-limited).</summary>
        Task<(bool Ok, string? Error, int CooldownSeconds)> ResendChallengeAsync(string challengeToken, string language = "en");
    }

    public class TwoFactorService : ITwoFactorService
    {
        private const int OtpTtlMinutes = 10;
        private const int ChallengeTtlMinutes = 15;
        private const int ResendCooldownSeconds = 60;
        private const int MaxOtpAttempts = 5;

        private readonly ApplicationDbContext _context;
        private readonly IForgotEmailService _email;
        private readonly ILogger<TwoFactorService> _logger;

        public TwoFactorService(
            ApplicationDbContext context,
            IForgotEmailService email,
            ILogger<TwoFactorService> logger)
        {
            _context = context;
            _email = email;
            _logger = logger;
        }

        public async Task<(string ChallengeToken, string MaskedEmail)> IssueAdminChallengeAsync(MainAdminUser admin, string language = "en")
        {
            var otp = GenerateOtp();
            var token = GenerateChallengeToken();
            var now = DateTime.UtcNow;

            admin.LoginOtpHash = HashOtp(otp);
            admin.LoginOtpExpiresAt = now.AddMinutes(OtpTtlMinutes);
            admin.LoginOtpAttempts = 0;
            admin.LoginOtpLastSentAt = now;
            admin.LoginChallengeToken = HashOtp(token);
            admin.LoginChallengeExpiresAt = now.AddMinutes(ChallengeTtlMinutes);

            await _context.SaveChangesAsync();

            try
            {
                await _email.SendEmailVerificationAsync(admin.Email, otp, admin.FirstName ?? "User", language, OtpTtlMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send 2FA OTP to admin {Email}", admin.Email);
            }

            return (token, MaskEmail(admin.Email));
        }

        public async Task<(string ChallengeToken, string MaskedEmail)> IssueUserChallengeAsync(User user, string language = "en")
        {
            var otp = GenerateOtp();
            var token = GenerateChallengeToken();
            var now = DateTime.UtcNow;

            user.LoginOtpHash = HashOtp(otp);
            user.LoginOtpExpiresAt = now.AddMinutes(OtpTtlMinutes);
            user.LoginOtpAttempts = 0;
            user.LoginOtpLastSentAt = now;
            user.LoginChallengeToken = HashOtp(token);
            user.LoginChallengeExpiresAt = now.AddMinutes(ChallengeTtlMinutes);

            await _context.SaveChangesAsync();

            try
            {
                await _email.SendEmailVerificationAsync(user.Email, otp, user.FirstName ?? "User", language, OtpTtlMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send 2FA OTP to user {Email}", user.Email);
            }

            return (token, MaskEmail(user.Email));
        }

        public async Task<(bool Ok, string? Error, int? AdminId, int? UserId)> VerifyChallengeAsync(string challengeToken, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(challengeToken) || string.IsNullOrWhiteSpace(otpCode))
                return (false, "invalid_challenge", null, null);

            var hashedChallenge = HashOtp(challengeToken);
            var now = DateTime.UtcNow;

            // Admin path
            var admin = await _context.MainAdminUsers
                .FirstOrDefaultAsync(a => a.LoginChallengeToken == hashedChallenge);
            if (admin != null)
            {
                if (admin.LoginChallengeExpiresAt == null || admin.LoginChallengeExpiresAt < now)
                    return (false, "challenge_expired", null, null);

                if (admin.LoginOtpAttempts >= MaxOtpAttempts)
                    return (false, "too_many_attempts", null, null);

                if (admin.LoginOtpExpiresAt == null || admin.LoginOtpExpiresAt < now)
                    return (false, "code_expired", null, null);

                var provided = HashOtp(otpCode);
                if (!ConstantTimeEquals(admin.LoginOtpHash ?? "", provided))
                {
                    admin.LoginOtpAttempts += 1;
                    await _context.SaveChangesAsync();
                    return (false, "invalid_code", null, null);
                }

                // Success - clear challenge
                admin.LoginOtpHash = null;
                admin.LoginOtpExpiresAt = null;
                admin.LoginOtpAttempts = 0;
                admin.LoginChallengeToken = null;
                admin.LoginChallengeExpiresAt = null;
                await _context.SaveChangesAsync();
                return (true, null, admin.Id, null);
            }

            // User path
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.LoginChallengeToken == hashedChallenge);
            if (user != null)
            {
                if (user.LoginChallengeExpiresAt == null || user.LoginChallengeExpiresAt < now)
                    return (false, "challenge_expired", null, null);

                if (user.LoginOtpAttempts >= MaxOtpAttempts)
                    return (false, "too_many_attempts", null, null);

                if (user.LoginOtpExpiresAt == null || user.LoginOtpExpiresAt < now)
                    return (false, "code_expired", null, null);

                var provided = HashOtp(otpCode);
                if (!ConstantTimeEquals(user.LoginOtpHash ?? "", provided))
                {
                    user.LoginOtpAttempts += 1;
                    await _context.SaveChangesAsync();
                    return (false, "invalid_code", null, null);
                }

                user.LoginOtpHash = null;
                user.LoginOtpExpiresAt = null;
                user.LoginOtpAttempts = 0;
                user.LoginChallengeToken = null;
                user.LoginChallengeExpiresAt = null;
                await _context.SaveChangesAsync();
                return (true, null, null, user.Id);
            }

            return (false, "invalid_challenge", null, null);
        }

        public async Task<(bool Ok, string? Error, int CooldownSeconds)> ResendChallengeAsync(string challengeToken, string language = "en")
        {
            if (string.IsNullOrWhiteSpace(challengeToken))
                return (false, "invalid_challenge", 0);

            var hashedChallenge = HashOtp(challengeToken);
            var now = DateTime.UtcNow;

            var admin = await _context.MainAdminUsers.FirstOrDefaultAsync(a => a.LoginChallengeToken == hashedChallenge);
            if (admin != null)
            {
                if (admin.LoginChallengeExpiresAt == null || admin.LoginChallengeExpiresAt < now)
                    return (false, "challenge_expired", 0);
                var since = (now - (admin.LoginOtpLastSentAt ?? DateTime.MinValue)).TotalSeconds;
                if (since < ResendCooldownSeconds)
                    return (false, "cooldown", (int)(ResendCooldownSeconds - since));
                await IssueAdminChallengeAsync(admin, language); // re-issue reuses fields
                return (true, null, ResendCooldownSeconds);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.LoginChallengeToken == hashedChallenge);
            if (user != null)
            {
                if (user.LoginChallengeExpiresAt == null || user.LoginChallengeExpiresAt < now)
                    return (false, "challenge_expired", 0);
                var since = (now - (user.LoginOtpLastSentAt ?? DateTime.MinValue)).TotalSeconds;
                if (since < ResendCooldownSeconds)
                    return (false, "cooldown", (int)(ResendCooldownSeconds - since));
                await IssueUserChallengeAsync(user, language);
                return (true, null, ResendCooldownSeconds);
            }

            return (false, "invalid_challenge", 0);
        }

        // ---- helpers ----
        private static string GenerateOtp()
        {
            var bytes = new byte[4];
            RandomNumberGenerator.Fill(bytes);
            var num = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
            return num.ToString("D6");
        }

        private static string GenerateChallengeToken()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        private static string HashOtp(string value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var result = 0;
            for (var i = 0; i < a.Length; i++) result |= a[i] ^ b[i];
            return result == 0;
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return email;
            var parts = email.Split('@');
            var local = parts[0];
            var domain = parts[1];
            if (local.Length <= 2) return $"{local[0]}***@{domain}";
            return $"{local[0]}{new string('•', Math.Max(1, local.Length - 2))}{local[^1]}@{domain}";
        }
    }
}
