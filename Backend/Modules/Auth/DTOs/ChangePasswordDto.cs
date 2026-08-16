using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Auth.DTOs
{
    public class ChangePasswordRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Current password is required")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "New password must be at least 8 characters")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare("NewPassword", ErrorMessage = "Password confirmation does not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
