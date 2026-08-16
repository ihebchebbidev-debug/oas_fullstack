using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApi.Modules.Auth.Models
{
    [Table("MainAdminUsers")]
    public class MainAdminUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginDate { get; set; }

        public bool OnboardingCompleted { get; set; } = false;

        [Column(TypeName = "text")]
        public string? AccessToken { get; set; }

        [Column(TypeName = "text")]
        public string? RefreshToken { get; set; }

        public DateTime? TokenExpiresAt { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(2)]
        public string? Country { get; set; }

        [MaxLength(100)]
        public string? Industry { get; set; } = "";

        [MaxLength(255)]
        public string? CompanyName { get; set; }

        [MaxLength(500)]
        public string? CompanyWebsite { get; set; }

        [MaxLength(500)]
        public string? CompanyLogoUrl { get; set; }

        [MaxLength(500)]
        public string? ProfilePictureUrl { get; set; }

        [Column(TypeName = "text")]
        public string? PreferencesJson { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        // Forgot Password Fields
        [MaxLength(6)]
        public string? OtpCode { get; set; }

        public DateTime? OtpExpiresAt { get; set; }

        [MaxLength(500)]
        public string? PasswordResetToken { get; set; }

        public DateTime? PasswordResetTokenExpiresAt { get; set; }

        // ---- Email Verification (migration 35) ----
        public bool EmailVerified { get; set; } = false;
        public DateTime? EmailVerifiedAt { get; set; }
        [MaxLength(128)]
        public string? EmailVerifyOtpHash { get; set; }
        public DateTime? EmailVerifyOtpExpiresAt { get; set; }
        public int EmailVerifyOtpAttempts { get; set; } = 0;
        public DateTime? EmailVerifyOtpLastSentAt { get; set; }

        // ---- Two-Factor Authentication (migration 36) ----
        public bool TwoFactorEnabled { get; set; } = false;
        public DateTime? TwoFactorEnabledAt { get; set; }
        [MaxLength(128)]
        public string? LoginOtpHash { get; set; }
        public DateTime? LoginOtpExpiresAt { get; set; }
        public int LoginOtpAttempts { get; set; } = 0;
        public DateTime? LoginOtpLastSentAt { get; set; }
        [MaxLength(128)]
        public string? LoginChallengeToken { get; set; }
        public DateTime? LoginChallengeExpiresAt { get; set; }
    }
}
