using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MyApi.Modules.Auth.DTOs
{
    public class LoginRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }

    public class SignupRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        [MaxLength(2)]
        public string Country { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Industry { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? CompanyName { get; set; }

        [MaxLength(500)]
        public string? CompanyWebsite { get; set; }

        public string? Preferences { get; set; } // JSON string
    }

    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public UserDto? User { get; set; }

        // ---- Two-Factor Authentication (migration 36) ----
        /// <summary>When true, client must complete the 2FA challenge before receiving tokens.</summary>
        [JsonPropertyName("requires2FA")]
        public bool Requires2FA { get; set; } = false;

        /// <summary>Short-lived opaque token identifying the pending 2FA challenge.</summary>
        [JsonPropertyName("challengeToken")]
        public string? ChallengeToken { get; set; }

        /// <summary>"admin" or "user" — tells the client which login endpoint to resume after 2FA.</summary>
        [JsonPropertyName("challengeUserType")]
        public string? ChallengeUserType { get; set; }

        /// <summary>Masked email the OTP was sent to (e.g. j***@example.com).</summary>
        [JsonPropertyName("maskedEmail")]
        public string? MaskedEmail { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Country { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? CompanyWebsite { get; set; }
        [JsonPropertyName("companyLogoUrl")]
        public string? CompanyLogoUrl { get; set; }
        [JsonPropertyName("profilePictureUrl")]
        public string? ProfilePictureUrl { get; set; }
        public string? Preferences { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool OnboardingCompleted { get; set; }
        [JsonPropertyName("emailVerified")]
        public bool EmailVerified { get; set; }
        [JsonPropertyName("twoFactorEnabled")]
        public bool TwoFactorEnabled { get; set; }
    }

    // ---- Two-Factor Authentication DTOs (migration 36) ----
    public class TwoFactorVerifyRequestDto
    {
        [Required]
        public string ChallengeToken { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string OtpCode { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = true;
    }

    public class TwoFactorResendRequestDto
    {
        [Required]
        public string ChallengeToken { get; set; } = string.Empty;

        [MaxLength(2)]
        public string Language { get; set; } = "en";
    }

    public class TwoFactorToggleRequestDto
    {
        [Required]
        public bool Enabled { get; set; }
    }


    public class RefreshTokenRequestDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class UpdateUserRequestDto
    {
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(2)]
        public string? Country { get; set; }

        [MaxLength(100)]
        public string? Industry { get; set; }

        [MaxLength(255)]
        public string? CompanyName { get; set; }

        [MaxLength(500)]
        public string? CompanyWebsite { get; set; }

        [MaxLength(500)]
        [JsonPropertyName("companyLogoUrl")]
        public string? CompanyLogoUrl { get; set; }

        [MaxLength(500)]
        [JsonPropertyName("profilePictureUrl")]
        public string? ProfilePictureUrl { get; set; }

        public string? Preferences { get; set; }
        
        public bool? OnboardingCompleted { get; set; }

        [JsonPropertyName("twoFactorEnabled")]
        public bool? TwoFactorEnabled { get; set; }
    }

    public class OAuthLoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response DTO for admin-exists endpoint with preferences for login page theming
    /// </summary>
    public class AdminExistsResultDto
    {
        public bool AdminExists { get; set; }
        public bool SignupAllowed { get; set; }
        public string Message { get; set; } = string.Empty;
        public AdminPreferencesDto? AdminPreferences { get; set; }
        [JsonPropertyName("companyLogoUrl")]
        public string? CompanyLogoUrl { get; set; }
    }

    /// <summary>
    /// Admin preferences for theming the login page
    /// </summary>
    public class AdminPreferencesDto
    {
        public string? Theme { get; set; }
        public string? Language { get; set; }
        public string? PrimaryColor { get; set; }
    }

    /// <summary>
    /// Request to initiate password reset - sends OTP email
    /// </summary>
    public class ForgotPasswordRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MaxLength(2)]
        public string Language { get; set; } = "en";
    }

    /// <summary>
    /// Request to verify OTP code
    /// </summary>
    public class VerifyOtpRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string OtpCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from OTP verification with reset token
    /// </summary>
    public class VerifyOtpResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ResetToken { get; set; }
    }

    /// <summary>
    /// Request to reset password with new password and reset token
    /// </summary>
    public class ResetPasswordRequestDto
    {
        [Required]
        public string ResetToken { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
