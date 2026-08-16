using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Shared.DTOs;
using MyApi.Modules.Users.DTOs;
using MyApi.Modules.Users.Services;
using MyApi.Modules.Auth.DTOs;
using System.Security.Claims;

namespace MyApi.Modules.Users.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Only authenticated MainAdminUsers can access
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _userService.GetAllUsersAsync();
                return Ok(new ApiResponse<UserListResponseDto>
                {
                    Success = true,
                    Message = "Users retrieved successfully",
                    Data = users
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error while retrieving users"
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new ApiResponse<UserResponseDto>
                {
                    Success = true,
                    Message = "User retrieved successfully",
                    Data = user
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error while retrieving user"
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                // Get the current user's ID and email from JWT claims
                var currentUserId = User?.FindFirst("UserId")?.Value ?? User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var currentUserEmail = User?.FindFirst(ClaimTypes.Email)?.Value;
                
                if (string.IsNullOrEmpty(currentUserId) && string.IsNullOrEmpty(currentUserEmail))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Unable to identify current user"
                    });
                }

                // Use ID if available, otherwise fall back to email
                var createdBy = !string.IsNullOrEmpty(currentUserId) ? $"admin:{currentUserId}" : currentUserEmail ?? "system";
                var newUser = await _userService.CreateUserAsync(createDto, createdBy);
                
                return CreatedAtAction(
                    nameof(GetUser), 
                    new { id = newUser.Id }, 
                    new ApiResponse<UserResponseDto>
                    {
                        Success = true,
                        Message = "User created successfully",
                        Data = newUser
                    });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error while creating user"
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateRegularUserRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                // Get the current user's email from JWT claims
                var currentUserEmail = User?.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(currentUserEmail))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Unable to identify current user"
                    });
                }

                var updatedUser = await _userService.UpdateUserAsync(id, updateDto, currentUserEmail);
                if (updatedUser == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new ApiResponse<UserResponseDto>
                {
                    Success = true,
                    Message = "User updated successfully",
                    Data = updatedUser
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error while updating user"
                });
            }
        }

        /// <summary>
        /// Update profile picture for a specific regular user
        /// </summary>
        [HttpPut("{id}/profile-picture")]
        public async Task<IActionResult> UpdateUserProfilePicture(int id, [FromBody] MyApi.Modules.Auth.DTOs.UpdateProfilePictureRequestDto request)
        {
            try
            {
                var currentUserEmail = User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(currentUserEmail))
                {
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "Unable to identify current user" });
                }

                var updatedUser = await _userService.UpdateUserProfilePictureAsync(id, request.ProfilePictureUrl, currentUserEmail);
                if (updatedUser == null)
                {
                    return NotFound(new ApiResponse<object> { Success = false, Message = "User not found" });
                }

                return Ok(new ApiResponse<UserResponseDto>
                {
                    Success = true,
                    Message = "Profile picture updated successfully",
                    Data = updatedUser
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile picture for user {UserId}", id);
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Internal error updating profile picture" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                // Get the current user's email from JWT claims
                var currentUserEmail = User?.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(currentUserEmail))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Unable to identify current user"
                    });
                }

                var deleted = await _userService.DeleteUserAsync(id, currentUserEmail);
                if (!deleted)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "User deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error while deleting user"
                });
            }
        }

        [HttpPost("{id}/change-password")]
        public async Task<IActionResult> ChangeUserPassword(int id, [FromBody] ChangeUserPasswordDto changePasswordDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                // Get the current user's email from JWT claims
                var currentUserEmail = User?.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(currentUserEmail))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Unable to identify current user"
                    });
                }

                var success = await _userService.ChangeUserPasswordAsync(id, changePasswordDto.NewPassword, currentUserEmail);
                if (!success)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Password changed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error while changing password"
                });
            }
        }

        [HttpGet("email/{email}")]
        public async Task<IActionResult> GetUserByEmail(string email)
        {
            try
            {
                var user = await _userService.GetUserByEmailAsync(email);
                if (user == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new ApiResponse<UserResponseDto>
                {
                    Success = true,
                    Message = "User retrieved successfully",
                    Data = user
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by email {Email}", email);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error while retrieving user"
                });
            }
        }

        [HttpGet("check-email")]
        public async Task<IActionResult> CheckEmailExists([FromQuery] string email, [FromQuery] int? excludeUserId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Email is required"
                    });
                }

                var result = await _userService.CheckEmailExistsAsync(email, excludeUserId);
                
                return Ok(new ApiResponse<CheckEmailResponseDto>
                {
                    Success = true,
                    Message = "Email check completed",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email existence {Email}", email);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error while checking email"
                });
            }
        }

        /// <summary>
        /// Initiates password reset by sending OTP to user's email
        /// </summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Email is required"
                    });
                }

                // Extract language from request headers or default to "en"
                var language = HttpContext.Request.Headers["Accept-Language"].ToString().StartsWith("fr") ? "fr" : "en";

                var (success, message) = await _userService.GenerateOtpAndSendEmailAsync(request.Email, language);

                return Ok(new ApiResponse<object>
                {
                    Success = success,
                    Message = message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in forgot password endpoint");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred during password reset request"
                });
            }
        }

        /// <summary>
        /// Verifies OTP and generates password reset token
        /// </summary>
        [HttpPost("verify-otp")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequestDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.OtpCode))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Email and OTP code are required"
                    });
                }

                var (success, message, resetToken) = await _userService.VerifyOtpAsync(request.Email, request.OtpCode);

                return Ok(new ApiResponse<VerifyOtpResponseDto>
                {
                    Success = success,
                    Message = message,
                    Data = new VerifyOtpResponseDto
                    {
                        Success = success,
                        Message = message,
                        ResetToken = resetToken
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in verify OTP endpoint");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred during OTP verification"
                });
            }
        }

        /// <summary>
        /// Resets user password using reset token
        /// </summary>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ResetToken) || 
                    string.IsNullOrWhiteSpace(request.NewPassword) || 
                    string.IsNullOrWhiteSpace(request.ConfirmPassword))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Reset token and passwords are required"
                    });
                }

                var (success, message) = await _userService.ResetPasswordAsync(
                    request.ResetToken,
                    request.NewPassword,
                    request.ConfirmPassword
                );

                return Ok(new ApiResponse<object>
                {
                    Success = success,
                    Message = message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in reset password endpoint");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred during password reset"
                });
            }
        }
    }
}
