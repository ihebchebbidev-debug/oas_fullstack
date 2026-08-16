using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Users.Models
{
    [Table("Users")]
    public class User : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        [Required]
        [MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        public DateTime? DeletedDate { get; set; }

        [MaxLength(100)]
        public string? DeletedBy { get; set; }

        [Column(TypeName = "text")]
        public string? AccessToken { get; set; }

        [Column(TypeName = "text")]
        public string? RefreshToken { get; set; }

        public DateTime? TokenExpiresAt { get; set; }

        [MaxLength(50)]
        public string? CurrentStatus { get; set; } = "offline";

        [Column(TypeName = "text")]
        public string? LocationJson { get; set; }

        [MaxLength(2)]
        public string? Country { get; set; } = "US";

        public DateTime? LastLoginAt { get; set; }

        // Legacy columns for backward compatibility
        [MaxLength(100)]
        public string? CreatedUser { get; set; } = "system";

        [MaxLength(100)]
        public string? ModifyUser { get; set; }

        public DateTime? ModifyDate { get; set; }

        [MaxLength(50)]
        public string? Role { get; set; } = "User";

        [Column(TypeName = "text")]
        public string? Skills { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(500)]
        public string? ProfilePictureUrl { get; set; }

        // Password Reset Fields (OTP & Reset Token)
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
        public DateTime? FirstLoginAt { get; set; }

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



        // Navigation properties
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public virtual ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
        public virtual UserPreferences? Preferences { get; set; }
    }
}
