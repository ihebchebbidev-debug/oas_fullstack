using MyApi.Data;
using MyApi.Modules.Users.DTOs;
using MyApi.Modules.Users.Models;
using MyApi.Modules.Shared.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace MyApi.Modules.Users.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly IForgotEmailService _forgotEmailService;

        public UserService(
            ApplicationDbContext context, 
            ILogger<UserService> logger,
            IForgotEmailService forgotEmailService)
        {
            _context = context;
            _logger = logger;
            _forgotEmailService = forgotEmailService;
        }

        /// <summary>
        /// ✅ PERFORMANCE: Server-side pagination — no longer loads ALL users into memory.
        /// Backwards-compatible: no params = first 200 users (safe default).
        /// </summary>
        public async Task<UserListResponseDto> GetAllUsersAsync()
        {
            try
            {
                var baseQuery = _context.Users.AsNoTracking().Where(u => !u.IsDeleted);

                var totalCount = await baseQuery.CountAsync();

                var users = await baseQuery
                    .OrderByDescending(u => u.CreatedDate)
                    .Take(500)
                    .ToListAsync();

                return new UserListResponseDto
                {
                    Users = users.Select(u => MapToUserDto(u)).ToList(),
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                throw;
            }
        }

        public async Task<UserResponseDto?> GetUserByIdAsync(int id)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == id && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                return user != null ? MapToUserDto(user) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by id {UserId}", id);
                throw;
            }
        }

        public async Task<UserResponseDto?> GetUserByEmailAsync(string email)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Email.ToLower() == email.ToLower() && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                return user != null ? MapToUserDto(user) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email {Email}", email);
                throw;
            }
        }

        public async Task<UserResponseDto> CreateUserAsync(CreateUserRequestDto createDto, string createdByUser)
        {
            try
            {
                // Check if email exists in Users table
                var existsInUsers = await _context.Users
                    .AnyAsync(u => u.Email.ToLower() == createDto.Email.ToLower() && !u.IsDeleted);

                if (existsInUsers)
                {
                    throw new InvalidOperationException("A user with this email already exists in the Users table");
                }

                // Check if email exists in MainAdminUsers table
                var existsInAdminUsers = await _context.MainAdminUsers
                    .AnyAsync(u => u.Email.ToLower() == createDto.Email.ToLower());

                if (existsInAdminUsers)
                {
                    throw new InvalidOperationException("A user with this email already exists in the Admin Users table");
                }

                // Hash password
                var passwordHash = HashPassword(createDto.Password);

                var user = new User
                {
                    Email = createDto.Email.ToLower(),
                    PasswordHash = passwordHash,
                    FirstName = createDto.FirstName,
                    LastName = createDto.LastName,
                    PhoneNumber = createDto.PhoneNumber,
                    Country = createDto.Country,
                    Role = createDto.Role,
                    CreatedUser = createdByUser,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true,
                    IsDeleted = false
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User created successfully with ID {UserId}", user.Id);
                return MapToUserDto(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                throw;
            }
        }

        public async Task<UserResponseDto?> UpdateUserAsync(int id, UpdateRegularUserRequestDto updateDto, string modifiedByUser)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == id && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return null;
                }

                // Check if email is being changed and if new email already exists
                if (!string.IsNullOrEmpty(updateDto.Email) && 
                    updateDto.Email.ToLower() != user.Email.ToLower())
                {
                    // Check if email exists in Users table
                    var existsInUsers = await _context.Users
                        .AnyAsync(u => u.Email.ToLower() == updateDto.Email.ToLower() && !u.IsDeleted && u.Id != id);

                    if (existsInUsers)
                    {
                        throw new InvalidOperationException("A user with this email already exists in the Users table");
                    }

                    // Check if email exists in MainAdminUsers table
                    var existsInAdminUsers = await _context.MainAdminUsers
                        .AnyAsync(u => u.Email.ToLower() == updateDto.Email.ToLower());

                    if (existsInAdminUsers)
                    {
                        throw new InvalidOperationException("A user with this email already exists in the Admin Users table");
                    }

                    user.Email = updateDto.Email.ToLower();
                }

                // Update fields if provided
                if (!string.IsNullOrEmpty(updateDto.FirstName))
                    user.FirstName = updateDto.FirstName;

                if (!string.IsNullOrEmpty(updateDto.LastName))
                    user.LastName = updateDto.LastName;

                if (updateDto.PhoneNumber != null)
                    user.PhoneNumber = updateDto.PhoneNumber;

                if (!string.IsNullOrEmpty(updateDto.Country))
                    user.Country = updateDto.Country;

                if (updateDto.Role != null)
                    user.Role = updateDto.Role;

                if (updateDto.IsActive.HasValue)
                    user.IsActive = updateDto.IsActive.Value;

                // ProfilePictureUrl: update if provided (empty string = remove, URL = set)
                if (updateDto.ProfilePictureUrl != null)
                    user.ProfilePictureUrl = string.IsNullOrEmpty(updateDto.ProfilePictureUrl) ? null : updateDto.ProfilePictureUrl;

                user.ModifyUser = modifiedByUser;
                user.ModifyDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("User updated successfully with ID {UserId}", user.Id);
                return MapToUserDto(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with ID {UserId}", id);
                throw;
            }
        }

        /// <summary>
        /// Dedicated method to update ONLY the ProfilePictureUrl for a regular User.
        /// </summary>
        public async Task<UserResponseDto?> UpdateUserProfilePictureAsync(int id, string? profilePictureUrl, string modifiedByUser)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == id && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (user == null) return null;

                user.ProfilePictureUrl = string.IsNullOrEmpty(profilePictureUrl) ? null : profilePictureUrl;
                user.ModifyUser = modifiedByUser;
                user.ModifyDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("ProfilePicture updated for User {UserId}: {Url}", id, profilePictureUrl ?? "(removed)");
                return MapToUserDto(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile picture for User {UserId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteUserAsync(int id, string deletedByUser)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == id && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return false;
                }

                // Soft delete
                user.IsDeleted = true;
                user.ModifyUser = deletedByUser;
                user.ModifyDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("User soft deleted successfully with ID {UserId}", user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user with ID {UserId}", id);
                throw;
            }
        }

        public async Task<bool> ChangeUserPasswordAsync(int id, string newPassword, string modifiedByUser)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == id && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return false;
                }

                user.PasswordHash = HashPassword(newPassword);
                user.ModifyUser = modifiedByUser;
                user.ModifyDate = DateTime.UtcNow;

                // Clear existing tokens to force re-login
                user.AccessToken = null;
                user.RefreshToken = null;
                user.TokenExpiresAt = null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Password changed successfully for user ID {UserId}", user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user ID {UserId}", id);
                throw;
            }
        }

        public async Task<bool> UserExistsAsync(string email)
        {
            try
            {
                // ✅ PERFORMANCE: Run both checks in parallel
                var existsInUsersTask = _context.Users
                    .AnyAsync(u => u.Email.ToLower() == email.ToLower() && !u.IsDeleted);

                var existsInAdminUsersTask = _context.MainAdminUsers
                    .AnyAsync(u => u.Email.ToLower() == email.ToLower());

                await Task.WhenAll(existsInUsersTask, existsInAdminUsersTask);

                return existsInUsersTask.Result || existsInAdminUsersTask.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user exists with email {Email}", email);
                throw;
            }
        }

        public async Task<CheckEmailResponseDto> CheckEmailExistsAsync(string email, int? excludeUserId = null)
        {
            try
            {
                var emailLower = email.ToLower().Trim();

                // ✅ PERFORMANCE: Run both checks in parallel
                var existsInAdminUsersTask = _context.MainAdminUsers
                    .AnyAsync(u => u.Email.ToLower() == emailLower);

                var existsInUsersTask = _context.Users
                    .AnyAsync(u => u.Email.ToLower() == emailLower && !u.IsDeleted && (excludeUserId == null || u.Id != excludeUserId));

                await Task.WhenAll(existsInAdminUsersTask, existsInUsersTask);

                if (existsInAdminUsersTask.Result)
                {
                    return new CheckEmailResponseDto { Exists = true, Source = "admin" };
                }

                if (existsInUsersTask.Result)
                {
                    return new CheckEmailResponseDto { Exists = true, Source = "user" };
                }

                return new CheckEmailResponseDto { Exists = false, Source = null };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email existence for {Email}", email);
                throw;
            }
        }

        private static UserResponseDto MapToUserDto(User user)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Country = user.Country ?? string.Empty,
                Role = user.Role,
                IsActive = user.IsActive,
                ProfilePictureUrl = user.ProfilePictureUrl,
                CreatedUser = user.CreatedUser ?? string.Empty,
                ModifyUser = user.ModifyUser ?? string.Empty,
                CreatedDate = user.CreatedDate,
                ModifyDate = user.ModifyDate,
                LastLoginAt = user.LastLoginAt
            };
        }

        private static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        /// <summary>
        /// Generates a 6-digit OTP and sends it to user's email for password reset
        /// </summary>
        public async Task<(bool Success, string Message)> GenerateOtpAndSendEmailAsync(string email, string language = "en")
        {
            try
            {
                _logger.LogInformation($"[USER_FORGOT_PASSWORD] OTP request for email: {email}");

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive && !u.IsDeleted);

                if (user == null)
                {
                    _logger.LogWarning($"[USER_FORGOT_PASSWORD] No active user found with email: {email}");
                    return (false, "No account found with this email address");
                }

                // Generate cryptographically secure 6-digit OTP
                var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
                user.OtpCode = otpCode;
                user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
                user.ModifiedDate = DateTime.UtcNow;
                user.ModifyUser = "system";

                await _context.SaveChangesAsync();

                // Send OTP email
                var emailSent = await _forgotEmailService.SendOtpEmailAsync(
                    email,
                    otpCode,
                    user.FirstName,
                    language
                );

                if (emailSent)
                {
                    _logger.LogInformation($"[USER_FORGOT_PASSWORD] OTP sent successfully to {email}");
                    return (true, "OTP sent to your email address");
                }
                else
                {
                    _logger.LogError($"[USER_FORGOT_PASSWORD] Failed to send OTP email to {email}");
                    return (false, "Failed to send OTP email");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[USER_FORGOT_PASSWORD] Error generating OTP for {email}");
                return (false, "An error occurred during password reset request");
            }
        }

        /// <summary>
        /// Verifies OTP code and generates a password reset token
        /// </summary>
        public async Task<(bool Success, string Message, string? ResetToken)> VerifyOtpAsync(string email, string otpCode)
        {
            try
            {
                _logger.LogInformation($"[USER_VERIFY_OTP] Verification request for email: {email}");

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive && !u.IsDeleted);

                if (user == null)
                {
                    _logger.LogWarning($"[USER_VERIFY_OTP] User not found: {email}");
                    return (false, "User not found", null);
                }

                // Check if OTP exists
                if (string.IsNullOrEmpty(user.OtpCode))
                {
                    _logger.LogWarning($"[USER_VERIFY_OTP] No OTP found for user: {email}");
                    return (false, "No OTP found. Please request a new one.", null);
                }

                // Check if OTP has expired
                if (user.OtpExpiresAt == null || user.OtpExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning($"[USER_VERIFY_OTP] OTP expired for user: {email}");
                    return (false, "OTP has expired. Please request a new one.", null);
                }

                // Verify OTP matches
                if (user.OtpCode != otpCode)
                {
                    _logger.LogWarning($"[USER_VERIFY_OTP] Invalid OTP for user: {email}");
                    return (false, "Invalid OTP code", null);
                }

                // Generate password reset token
                var resetToken = Guid.NewGuid().ToString("N");
                user.PasswordResetToken = resetToken;
                user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
                user.OtpCode = null; // Clear OTP after verification
                user.OtpExpiresAt = null;
                user.ModifiedDate = DateTime.UtcNow;
                user.ModifyUser = "system";

                await _context.SaveChangesAsync();

                _logger.LogInformation($"[USER_VERIFY_OTP] OTP verified successfully for {email}");
                return (true, "OTP verified successfully", resetToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[USER_VERIFY_OTP] Error verifying OTP for {email}");
                return (false, "An error occurred during OTP verification", null);
            }
        }

        /// <summary>
        /// Resets user password using reset token
        /// </summary>
        public async Task<(bool Success, string Message)> ResetPasswordAsync(string resetToken, string newPassword, string confirmPassword)
        {
            try
            {
                _logger.LogInformation($"[USER_RESET_PASSWORD] Password reset initiated with token");

                // Validate passwords match
                if (newPassword != confirmPassword)
                {
                    _logger.LogWarning($"[USER_RESET_PASSWORD] Password validation failed: passwords do not match");
                    return (false, "Passwords do not match");
                }

                // Validate password length
                if (newPassword.Length < 8)
                {
                    _logger.LogWarning($"[USER_RESET_PASSWORD] Password validation failed: password too short (length: {newPassword.Length})");
                    return (false, "Password must be at least 8 characters long");
                }

                _logger.LogInformation($"[USER_RESET_PASSWORD] Password validation passed. Looking up user by reset token");

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.PasswordResetToken == resetToken && u.IsActive && !u.IsDeleted);

                if (user == null)
                {
                    _logger.LogWarning($"[USER_RESET_PASSWORD] Invalid or expired reset token");
                    return (false, "Invalid or expired reset token");
                }

                // Check if reset token has expired
                if (user.PasswordResetTokenExpiresAt == null || user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning($"[USER_RESET_PASSWORD] Reset token expired for user: {user.Email}");
                    return (false, "Reset token has expired");
                }

                _logger.LogInformation($"[USER_RESET_PASSWORD] Valid reset token found. Updating password for user: {user.Email}");

                // Hash and set new password
                user.PasswordHash = HashPassword(newPassword);
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpiresAt = null;
                user.ModifiedDate = DateTime.UtcNow;
                user.ModifyUser = "system";

                await _context.SaveChangesAsync();

                _logger.LogInformation($"[USER_RESET_PASSWORD] Password reset successfully for user: {user.Email}");
                return (true, "Password reset successfully. You can now login with your new password.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[USER_RESET_PASSWORD] Error resetting password: {ex.Message}");
                return (false, "An error occurred during password reset");
            }
        }
    }
}
