using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Users.DTOs
{
    public class CreateUserRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        [Required]
        [MaxLength(2)]
        public string Country { get; set; } = string.Empty;

        public string? Role { get; set; }
    }

    public class UpdateRegularUserRequestDto
    {
        [EmailAddress]
        public string? Email { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string? PhoneNumber { get; set; }

        [MaxLength(2)]
        public string? Country { get; set; }

        public string? Role { get; set; }

        public bool? IsActive { get; set; }

        [MaxLength(500)]
        public string? ProfilePictureUrl { get; set; }
    }

    public class UserResponseDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Country { get; set; } = string.Empty;
        public string? Role { get; set; }
        public bool IsActive { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string CreatedUser { get; set; } = string.Empty;
        public string? ModifyUser { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifyDate { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class UserListResponseDto
    {
        public List<UserResponseDto> Users { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class ChangeUserPasswordDto
    {
        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class CheckEmailResponseDto
    {
        public bool Exists { get; set; }
        public string? Source { get; set; } // "admin" or "user"
    }
}
