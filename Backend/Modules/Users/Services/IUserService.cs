using MyApi.Modules.Users.DTOs;

namespace MyApi.Modules.Users.Services
{
    public interface IUserService
    {
        Task<UserListResponseDto> GetAllUsersAsync();
        Task<UserResponseDto?> GetUserByIdAsync(int id);
        Task<UserResponseDto?> GetUserByEmailAsync(string email);
        Task<UserResponseDto> CreateUserAsync(CreateUserRequestDto createDto, string createdByUser);
        Task<UserResponseDto?> UpdateUserAsync(int id, UpdateRegularUserRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteUserAsync(int id, string deletedByUser);
        Task<bool> ChangeUserPasswordAsync(int id, string newPassword, string modifiedByUser);
        Task<bool> UserExistsAsync(string email);
        Task<UserResponseDto?> UpdateUserProfilePictureAsync(int id, string? profilePictureUrl, string modifiedByUser);
        Task<CheckEmailResponseDto> CheckEmailExistsAsync(string email, int? excludeUserId = null);

        // Password Reset / OTP Methods
        /// <summary>
        /// Generates an OTP code and sends it to user's email for password reset
        /// </summary>
        Task<(bool Success, string Message)> GenerateOtpAndSendEmailAsync(string email, string language = "en");

        /// <summary>
        /// Verifies OTP code and generates a password reset token
        /// </summary>
        Task<(bool Success, string Message, string? ResetToken)> VerifyOtpAsync(string email, string otpCode);

        /// <summary>
        /// Resets user password using reset token
        /// </summary>
        Task<(bool Success, string Message)> ResetPasswordAsync(string resetToken, string newPassword, string confirmPassword);
    }
}
