using MyApi.Data;
using MyApi.Modules.Auth.DTOs;
using MyApi.Modules.Auth.Models;
using MyApi.Modules.Users.Models;
using MyApi.Modules.WorkflowEngine.Services;
using MyApi.Modules.Shared.Services;
using MyApi.Modules.Tenants.Models;
using MyApi.Modules.Tenants.Services;
using MyApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MyApi.Modules.Auth.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginRequestDto loginDto);
        Task<AuthResponseDto> UserLoginAsync(LoginRequestDto loginDto);
        Task<AuthResponseDto> SignupAsync(SignupRequestDto signupDto);
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
        Task<UserDto?> GetUserByIdAsync(int userId);
        Task<UserDto?> GetUserByEmailAsync(string email);
        Task<IEnumerable<UserDto>> GetAllAdminUsersAsync();
        Task<AuthResponseDto> OAuthLoginAsync(string email);
        Task<AuthResponseDto> UpdateUserAsync(int userId, UpdateUserRequestDto updateDto);
        Task<AuthResponseDto> UpdateProfilePictureAsync(int userId, string? profilePictureUrl);
        Task<AuthResponseDto> ChangePasswordAsync(int userId, ChangePasswordRequestDto changePasswordDto);
        Task<bool> LogoutAsync(int userId);
        Task<bool> AdminExistsAsync();
        Task<AdminExistsResultDto> GetAdminExistsWithPreferencesAsync();
        Task<string?> GetCompanyLogoUrlAsync();
        Task<(System.IO.Stream Stream, string ContentType, string FileName)?> GetCompanyLogoFileAsync(int documentId);
        Task<AuthResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request);
        Task<VerifyOtpResponseDto> VerifyOtpAsync(VerifyOtpRequestDto request);
        Task<AuthResponseDto> ResetPasswordAsync(ResetPasswordRequestDto request);
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private readonly IDefaultWorkflowSeeder _workflowSeeder;
        private readonly IForgotEmailService _forgotEmailService;
        private readonly TenantSeeder _tenantSeeder;
        private readonly ITwoFactorService _twoFactorService;

        public AuthService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<AuthService> logger,
            IDefaultWorkflowSeeder workflowSeeder,
            IForgotEmailService forgotEmailService,
            TenantSeeder tenantSeeder,
            ITwoFactorService twoFactorService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _workflowSeeder = workflowSeeder;
            _forgotEmailService = forgotEmailService;
            _tenantSeeder = tenantSeeder;
            _twoFactorService = twoFactorService;
        }


        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto loginDto)
        {
            try
            {
                // First, try to find user in MainAdminUsers table
                var adminUser = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == loginDto.Email.ToLower() && u.IsActive);

                if (adminUser != null && VerifyPassword(loginDto.Password, adminUser.PasswordHash))
                {
                    // 2FA gate — do not issue tokens if 2FA is enabled; return challenge instead.
                    if (adminUser.TwoFactorEnabled)
                    {
                        var (challengeToken, masked) = await _twoFactorService.IssueAdminChallengeAsync(adminUser);
                        return new AuthResponseDto
                        {
                            Success = false,
                            Requires2FA = true,
                            ChallengeToken = challengeToken,
                            ChallengeUserType = "admin",
                            MaskedEmail = masked,
                            Message = "Two-factor authentication required",
                        };
                    }

                    var (accessToken, refreshToken, expiresAt) = GenerateTokensAsync(adminUser);

                    // Update admin user login info
                    adminUser.LastLoginAt = DateTime.UtcNow;
                    adminUser.LastLoginDate = DateTime.UtcNow;
                    adminUser.AccessToken = accessToken;
                    adminUser.RefreshToken = refreshToken;
                    adminUser.TokenExpiresAt = expiresAt;
                    adminUser.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();


                    return new AuthResponseDto
                    {
                        Success = true,
                        Message = "Login successful",
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresAt = expiresAt,
                        User = MapToUserDto(adminUser)
                    };
                }

                // If not found in MainAdminUsers, try Users table
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == loginDto.Email.ToLower() && u.IsActive && !u.IsDeleted);

                if (user != null && VerifyPassword(loginDto.Password, user.PasswordHash))
                {
                    // 2FA gate — issue challenge instead of tokens if user opted in.
                    if (user.TwoFactorEnabled)
                    {
                        var (challengeToken, masked) = await _twoFactorService.IssueUserChallengeAsync(user);
                        return new AuthResponseDto
                        {
                            Success = false,
                            Requires2FA = true,
                            ChallengeToken = challengeToken,
                            ChallengeUserType = "user",
                            MaskedEmail = masked,
                            Message = "Two-factor authentication required",
                        };
                    }

                    var canSwitchLoginAsync = await UserCanSwitchCompanyAsync(user.Id);
                    var (accessToken, refreshToken, expiresAt) = GenerateUserTokensAsync(user, canSwitchLoginAsync);



                    // Update regular user login info
                    user.LastLoginAt = DateTime.UtcNow;
                    user.AccessToken = accessToken;
                    user.RefreshToken = refreshToken;
                    user.TokenExpiresAt = expiresAt;
                    user.ModifiedDate = DateTime.UtcNow;
                    user.ModifiedBy = user.Email;

                    await _context.SaveChangesAsync();

                    return new AuthResponseDto
                    {
                        Success = true,
                        Message = "Login successful",
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresAt = expiresAt,
                        User = MapUserToUserDto(user)
                    };
                }

                // No valid user found in either table
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid email or password"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during unified login for email: {Email}", loginDto.Email);
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during login"
                };
            }
        }

        public async Task<AuthResponseDto> UserLoginAsync(LoginRequestDto loginDto)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == loginDto.Email.ToLower() && u.IsActive && !u.IsDeleted);

                if (user == null || !VerifyPassword(loginDto.Password, user.PasswordHash))
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    };
                }

                // Check if user has at least one active role assigned
                var hasActiveRole = await _context.UserRoles
                    .AnyAsync(ur => ur.UserId == user.Id && ur.IsActive);

                if (!hasActiveRole)
                {
                    _logger.LogWarning("User {Email} attempted to login without any assigned roles", user.Email);
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Access denied. No role has been assigned to your account. Please contact your administrator."
                    };
                }

                // Get the user's primary role name for the token
                var userRole = await _context.UserRoles
                    .Where(ur => ur.UserId == user.Id && ur.IsActive)
                    .Include(ur => ur.Role)
                    .Select(ur => ur.Role!.Name)
                    .FirstOrDefaultAsync();

                // Update user's Role field with the actual role name
                if (!string.IsNullOrEmpty(userRole))
                {
                    user.Role = userRole;
                }

                // 2FA gate — issue challenge instead of tokens if user opted in.
                if (user.TwoFactorEnabled)
                {
                    var (challengeToken2fa, masked2fa) = await _twoFactorService.IssueUserChallengeAsync(user);
                    return new AuthResponseDto
                    {
                        Success = false,
                        Requires2FA = true,
                        ChallengeToken = challengeToken2fa,
                        ChallengeUserType = "user",
                        MaskedEmail = masked2fa,
                        Message = "Two-factor authentication required",
                    };
                }

                var canSwitchCompany = await UserCanSwitchCompanyAsync(user.Id);
                var (accessToken, refreshToken, expiresAt) = GenerateUserTokensAsync(user, canSwitchCompany);

                // Update user login info
                user.LastLoginAt = DateTime.UtcNow;
                user.AccessToken = accessToken;
                user.RefreshToken = refreshToken;
                user.TokenExpiresAt = expiresAt;
                user.ModifiedDate = DateTime.UtcNow;
                user.ModifiedBy = user.Email;


                await _context.SaveChangesAsync();

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Login successful",
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt,
                    User = MapUserToUserDto(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login for email: {Email}", loginDto.Email);
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during login"
                };
            }
        }

        public async Task<bool> AdminExistsAsync()
        {
            try
            {
                return await _context.MainAdminUsers.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if admin exists");
                return false;
            }
        }

        public async Task<AdminExistsResultDto> GetAdminExistsWithPreferencesAsync()
        {
            try
            {
                var admin = await _context.MainAdminUsers.FirstOrDefaultAsync();
                
                if (admin == null)
                {
                    return new AdminExistsResultDto
                    {
                        AdminExists = false,
                        SignupAllowed = true,
                        Message = "No administrator account found. Please create one."
                    };
                }

                // Extract theme, language, primaryColor from PreferencesJson
                AdminPreferencesDto? adminPreferences = null;
                if (!string.IsNullOrEmpty(admin.PreferencesJson))
                {
                    try
                    {
                        var jsonToParse = admin.PreferencesJson;
                        // Handle double-serialized JSON strings
                        if (jsonToParse.StartsWith("\"") && jsonToParse.EndsWith("\""))
                        {
                            try { jsonToParse = System.Text.Json.JsonSerializer.Deserialize<string>(jsonToParse) ?? jsonToParse; }
                            catch { }
                        }

                        using var doc = System.Text.Json.JsonDocument.Parse(jsonToParse);
                        var root = doc.RootElement;
                        
                        if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            adminPreferences = new AdminPreferencesDto
                            {
                                Theme = root.TryGetProperty("theme", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String ? t.GetString() : null,
                                Language = root.TryGetProperty("language", out var l) && l.ValueKind == System.Text.Json.JsonValueKind.String ? l.GetString() : null,
                                PrimaryColor = root.TryGetProperty("primaryColor", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.String ? c.GetString() : null
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse admin PreferencesJson");
                    }
                }

                // Resolve company logo: tenant-specific first, then fallback to admin's logo
                string? resolvedLogoUrl = admin.CompanyLogoUrl;
                var tenantId = _context.GetTenantId();
                if (tenantId > 0)
                {
                    var tenantLogo = await _context.Tenants
                        .Where(t => t.Id == tenantId && t.IsActive)
                        .Select(t => t.CompanyLogoUrl)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(tenantLogo))
                        resolvedLogoUrl = tenantLogo;
                }

                return new AdminExistsResultDto
                {
                    AdminExists = true,
                    SignupAllowed = false,
                    Message = "An administrator account exists. Please login.",
                    AdminPreferences = adminPreferences,
                    CompanyLogoUrl = resolvedLogoUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if admin exists with preferences");
                return new AdminExistsResultDto
                {
                    AdminExists = false,
                    SignupAllowed = true,
                    Message = "Error checking admin status"
                };
            }
        }

        /// <summary>
        /// Get the company logo URL/reference from the first admin user.
        /// Public — no auth required.
        /// </summary>
        public async Task<string?> GetCompanyLogoUrlAsync()
        {
            try
            {
                // Check if there's a current tenant with its own logo
                var tenantId = _context.GetTenantId();
                if (tenantId > 0)
                {
                    var tenantLogo = await _context.Tenants
                        .Where(t => t.Id == tenantId && t.IsActive)
                        .Select(t => t.CompanyLogoUrl)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(tenantLogo))
                        return tenantLogo;
                }

                // Fallback to MainAdminUser's company logo
                var admin = await _context.MainAdminUsers
                    .Where(a => a.IsActive)
                    .Select(a => a.CompanyLogoUrl)
                    .FirstOrDefaultAsync();
                return admin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company logo URL");
                return null;
            }
        }

        /// <summary>
        /// Get the company logo file stream for a given document ID.
        /// Used by the public /api/Auth/company-logo endpoint.
        /// </summary>
        public async Task<(System.IO.Stream Stream, string ContentType, string FileName)?> GetCompanyLogoFileAsync(int documentId)
        {
            try
            {
                var doc = await _context.Documents.FindAsync(documentId);
                if (doc == null) return null;

                // Resolve file path (same logic as DocumentsController)
                var relative = doc.FilePath.TrimStart('/');
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), relative);

                if (!System.IO.File.Exists(fullPath)) return null;

                var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                return (stream, doc.ContentType ?? "image/png", doc.OriginalName ?? doc.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company logo file {DocumentId}", documentId);
                return null;
            }
        }

        public async Task<AuthResponseDto> SignupAsync(SignupRequestDto signupDto)
        {
            try
            {
                _logger.LogInformation("Starting signup process for email: {Email}", signupDto.Email);
                
                // Check if any admin user already exists - block signup if so
                var adminExists = await _context.MainAdminUsers.AnyAsync();
                if (adminExists)
                {
                    _logger.LogWarning("Signup blocked: An admin user already exists. Email attempted: {Email}", signupDto.Email);
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Signup is disabled. An administrator account already exists. Please login instead."
                    };
                }
                
                // Check if email exists in MainAdminUsers table
                _logger.LogInformation("Checking if admin user exists for email: {Email}", signupDto.Email);
                var existingAdminUser = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == signupDto.Email.ToLower());

                if (existingAdminUser != null)
                {
                    _logger.LogWarning("Admin user already exists with email: {Email}", signupDto.Email);
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "A user with this email already exists"
                    };
                }

                // Note: Users table check removed - admin signup only uses MainAdminUsers table
                // Regular user management is handled separately

                // Create new admin user
                _logger.LogInformation("Hashing password for admin user: {Email}", signupDto.Email);
                string hashedPassword;
                try
                {
                    hashedPassword = signupDto.Password == "nopassword" ? "nopassword" : HashPassword(signupDto.Password);
                }
                catch (Exception hashEx)
                {
                    _logger.LogError(hashEx, "Password hashing failed for email: {Email}", signupDto.Email);
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = $"Password hashing failed: {hashEx.Message}"
                    };
                }
                
                _logger.LogInformation("Creating new admin user object for email: {Email}", signupDto.Email);
                
                // Reset the MainAdminUsers Id sequence so the first admin always gets Id=1
                // This handles cases where previous entries were deleted but the sequence advanced
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "SELECT setval('\"MainAdminUsers_Id_seq\"', 1, false)");
                    _logger.LogInformation("MainAdminUsers Id sequence reset to 1 for first admin signup");
                }
                catch (Exception seqEx)
                {
                    _logger.LogWarning(seqEx, "Could not reset MainAdminUsers sequence (non-critical)");
                }
                
                var newUser = new MainAdminUser
                {
                    Email = signupDto.Email.ToLower(),
                    Username = signupDto.Email.ToLower(), // Use email as username
                    PasswordHash = hashedPassword,
                    FirstName = signupDto.FirstName,
                    LastName = signupDto.LastName,
                    PhoneNumber = signupDto.PhoneNumber,
                    Country = signupDto.Country,
                    Industry = signupDto.Industry ?? "",
                    CompanyName = signupDto.CompanyName,
                    CompanyWebsite = signupDto.CompanyWebsite,
                    PreferencesJson = signupDto.Preferences,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    OnboardingCompleted = false
                };

                _logger.LogInformation("Adding admin user to database context for email: {Email}", signupDto.Email);
                _context.MainAdminUsers.Add(newUser);
                
                _logger.LogInformation("Saving changes to database for email: {Email}", signupDto.Email);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Database save failed for email: {Email}", signupDto.Email);
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = $"Database save failed: {dbEx.Message}"
                    };
                }

                _logger.LogInformation("Generating tokens for new admin user: {Email}", signupDto.Email);
                (string accessToken, string refreshToken, DateTime expiresAt) tokens;
                try
                {
                    tokens = GenerateTokensAsync(newUser);
                }
                catch (Exception tokenEx)
                {
                    _logger.LogError(tokenEx, "Token generation failed for email: {Email}", signupDto.Email);
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = $"Token generation failed: {tokenEx.Message}"
                    };
                }

                // Update user with tokens
                _logger.LogInformation("Updating admin user with tokens for email: {Email}", signupDto.Email);
                newUser.AccessToken = tokens.accessToken;
                newUser.RefreshToken = tokens.refreshToken;
                newUser.TokenExpiresAt = tokens.expiresAt;
                newUser.LastLoginAt = DateTime.UtcNow;
                newUser.LastLoginDate = DateTime.UtcNow;

                _logger.LogInformation("Saving token updates to database for email: {Email}", signupDto.Email);
                await _context.SaveChangesAsync();

                // Seed default workflow for fresh admin user
                _logger.LogInformation("Seeding default workflow for new admin: {Email}", signupDto.Email);
                await _workflowSeeder.SeedDefaultWorkflowAsync(signupDto.Email);

                _logger.LogInformation("Admin user registration completed successfully for email: {Email}", signupDto.Email);
                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Account created successfully",
                    AccessToken = tokens.accessToken,
                    RefreshToken = tokens.refreshToken,
                    ExpiresAt = tokens.expiresAt,
                    User = MapToUserDto(newUser)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during signup for email: {Email}. Exception: {Exception}", signupDto.Email, ex.ToString());
                return new AuthResponseDto
                {
                    Success = false,
                    Message = $"Signup error: {ex.GetType().Name} - {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Refreshes the access token for MainAdminUser or regular User.
        /// Checks MainAdminUsers first, then Users (with IgnoreQueryFilters for tenant-agnostic lookup).
        /// </summary>
        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                // 1. Try MainAdminUsers (admin/company owner)
                var adminUser = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.IsActive);

                if (adminUser != null)
                {
                    if (adminUser.TokenExpiresAt < DateTime.UtcNow)
                    {
                        return new AuthResponseDto
                        {
                            Success = false,
                            Message = "Invalid or expired refresh token"
                        };
                    }

                    var (newAccessToken, newRefreshToken, expiresAt) = GenerateTokensAsync(adminUser);

                    adminUser.AccessToken = newAccessToken;
                    adminUser.RefreshToken = newRefreshToken;
                    adminUser.TokenExpiresAt = expiresAt;
                    adminUser.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    return new AuthResponseDto
                    {
                        Success = true,
                        Message = "Token refreshed successfully",
                        AccessToken = newAccessToken,
                        RefreshToken = newRefreshToken,
                        ExpiresAt = expiresAt,
                        User = MapToUserDto(adminUser)
                    };
                }

                // 2. Try Users table (regular tenant users) - IgnoreQueryFilters for tenant-agnostic lookup
                var regularUser = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.IsActive && !u.IsDeleted);

                if (regularUser != null)
                {
                    if (regularUser.TokenExpiresAt.HasValue && regularUser.TokenExpiresAt.Value < DateTime.UtcNow)
                    {
                        return new AuthResponseDto
                        {
                            Success = false,
                            Message = "Invalid or expired refresh token"
                        };
                    }

                    var canSwitchRefresh = await UserCanSwitchCompanyAsync(regularUser.Id);
                    var (userAccessToken, userRefreshToken, userExpiresAt) = GenerateUserTokensAsync(regularUser, canSwitchRefresh);


                    regularUser.AccessToken = userAccessToken;
                    regularUser.RefreshToken = userRefreshToken;
                    regularUser.TokenExpiresAt = userExpiresAt;
                    regularUser.ModifiedDate = DateTime.UtcNow;
                    regularUser.ModifyUser = regularUser.Email;

                    await _context.SaveChangesAsync();

                    return new AuthResponseDto
                    {
                        Success = true,
                        Message = "Token refreshed successfully",
                        AccessToken = userAccessToken,
                        RefreshToken = userRefreshToken,
                        ExpiresAt = userExpiresAt,
                        User = MapUserToUserDto(regularUser)
                    };
                }

                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid or expired refresh token"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during token refresh"
                };
            }
        }

        public async Task<UserDto?> GetUserByIdAsync(int userId)
        {
            try
            {
                var user = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                return user != null ? MapToUserDto(user) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
                return null;
            }
        }

        public async Task<IEnumerable<UserDto>> GetAllAdminUsersAsync()
        {
            try
            {
                var adminUsers = await _context.MainAdminUsers
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToListAsync();

                return adminUsers.Select(MapToUserDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all admin users");
                return Enumerable.Empty<UserDto>();
            }
        }


        public async Task<AuthResponseDto> UpdateUserAsync(int userId, UpdateUserRequestDto updateDto)
        {
            try
            {
                var user = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                // Update user properties
                if (!string.IsNullOrEmpty(updateDto.FirstName))
                    user.FirstName = updateDto.FirstName;
                if (!string.IsNullOrEmpty(updateDto.LastName))
                    user.LastName = updateDto.LastName;
                if (!string.IsNullOrEmpty(updateDto.PhoneNumber))
                    user.PhoneNumber = updateDto.PhoneNumber;
                if (!string.IsNullOrEmpty(updateDto.Country))
                    user.Country = updateDto.Country;
                if (!string.IsNullOrEmpty(updateDto.Industry))
                    user.Industry = updateDto.Industry;
                if (!string.IsNullOrEmpty(updateDto.CompanyName))
                    user.CompanyName = updateDto.CompanyName;
                if (!string.IsNullOrEmpty(updateDto.CompanyWebsite))
                    user.CompanyWebsite = updateDto.CompanyWebsite;
                // CompanyLogoUrl: update if provided (empty string = remove, URL = set)
                if (updateDto.CompanyLogoUrl != null)
                    user.CompanyLogoUrl = string.IsNullOrEmpty(updateDto.CompanyLogoUrl) ? null : updateDto.CompanyLogoUrl;
                // ProfilePictureUrl: update if provided (empty string = remove, URL = set)
                if (updateDto.ProfilePictureUrl != null)
                    user.ProfilePictureUrl = string.IsNullOrEmpty(updateDto.ProfilePictureUrl) ? null : updateDto.ProfilePictureUrl;
                if (!string.IsNullOrEmpty(updateDto.Preferences))
                    user.PreferencesJson = updateDto.Preferences;
                if (updateDto.TwoFactorEnabled.HasValue)
                    user.TwoFactorEnabled = updateDto.TwoFactorEnabled.Value;
                if (updateDto.OnboardingCompleted.HasValue)
                {
                    bool wasAlreadyCompleted = user.OnboardingCompleted;
                    user.OnboardingCompleted = updateDto.OnboardingCompleted.Value;
                    
                    // If Onboarding has just been completed, create the default tenant
                    if (updateDto.OnboardingCompleted.Value && !wasAlreadyCompleted)
                    {
                        var hasTenants = await _context.Tenants.AnyAsync(t => t.MainAdminUserId == user.Id);
                        
                        if (!hasTenants)
                        {
                            var companyNameStr = !string.IsNullOrWhiteSpace(user.CompanyName) ? user.CompanyName : "Default Company";
                            
                            // Create a url-friendly slug
                            string slug = System.Text.RegularExpressions.Regex.Replace(companyNameStr.ToLower(), @"[^a-z0-9\s-]", "");
                            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-").Trim('-');
                            
                            // Handle potential slug collision
                            var slugExists = await _context.Tenants.AnyAsync(t => t.Slug == slug);
                            if (slugExists) slug += "-" + user.Id;
                            
                            var newTenant = new Tenant
                            {
                                MainAdminUserId = user.Id,
                                Slug = slug,
                                CompanyName = companyNameStr,
                                CompanyLogoUrl = user.CompanyLogoUrl,
                                CompanyWebsite = user.CompanyWebsite,
                                CompanyCountry = user.Country,
                                Industry = user.Industry,
                                IsActive = true,
                                IsDefault = true,
                                CreatedAt = DateTime.UtcNow
                            };
                            
                            _context.Tenants.Add(newTenant);
                            
                            // Save so we obtain an Id for the Tenant Seeder
                            await _context.SaveChangesAsync();
                            
                            try
                            {
                                await _tenantSeeder.SeedForNewTenantAsync(newTenant.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Tenant seeding failed for auto-created tenant {Slug} (Id={Id})", newTenant.Slug, newTenant.Id);
                            }
                            
                            TenantSlugCache.Refresh(_context);
                            _logger.LogInformation("Auto-created default Tenant {Slug} for new admin {UserId}", newTenant.Slug, user.Id);
                        }
                    }
                }

                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "User updated successfully",
                    User = MapToUserDto(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", userId);
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during user update"
                };
            }
        }

        /// <summary>
        /// Dedicated method to update ONLY the ProfilePictureUrl for MainAdminUser.
        /// This avoids any issues with the general UpdateUserAsync method.
        /// </summary>
        public async Task<AuthResponseDto> UpdateProfilePictureAsync(int userId, string? profilePictureUrl)
        {
            try
            {
                var user = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning("UpdateProfilePicture: MainAdminUser not found for ID {UserId}", userId);
                    return new AuthResponseDto { Success = false, Message = "User not found" };
                }

                user.ProfilePictureUrl = string.IsNullOrEmpty(profilePictureUrl) ? null : profilePictureUrl;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("ProfilePicture updated for MainAdminUser {UserId}: {Url}", userId, profilePictureUrl ?? "(removed)");

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Profile picture updated successfully",
                    User = MapToUserDto(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile picture for MainAdminUser {UserId}", userId);
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred updating profile picture: " + ex.Message
                };
            }
        }

        public async Task<AuthResponseDto> ChangePasswordAsync(int userId, ChangePasswordRequestDto changePasswordDto)
        {
            try
            {
                var user = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                // Verify current password
                if (!VerifyPassword(changePasswordDto.CurrentPassword, user.PasswordHash))
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Current password is incorrect"
                    };
                }

                // Hash new password
                user.PasswordHash = HashPassword(changePasswordDto.NewPassword);
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Password changed successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserId}", userId);
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred while changing password"
                };
            }
        }

        public async Task<bool> LogoutAsync(int userId)
        {
            try
            {
                var adminUser = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (adminUser != null)
                {
                    adminUser.AccessToken = null;
                    adminUser.RefreshToken = null;
                    adminUser.TokenExpiresAt = null;
                    adminUser.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return true;
                }

                var regularUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted);

                if (regularUser != null)
                {
                    regularUser.AccessToken = null;
                    regularUser.RefreshToken = null;
                    regularUser.ModifiedDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for user: {UserId}", userId);
                return false;
            }
        }

        // Generate tokens for MainAdminUser (Id=1 always)
        private (string accessToken, string refreshToken, DateTime expiresAt) GenerateTokensAsync(MainAdminUser user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "YourSuperSecretKeyHere12345";
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "FlowServiceBackend";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "FlowServiceFrontend";

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim("UserId", user.Id.ToString()),
                new Claim("FirstName", user.FirstName),
                new Claim("LastName", user.LastName),
                new Claim("Industry", user.Industry ?? ""),
                new Claim("UserType", "MainAdminUser"),
                new Claim("login_type", "admin")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Long-lived token (10 years) — matches ValidateLifetime=false so users
            // are never asked to reconnect.
            var expiresAt = DateTime.UtcNow.AddYears(10);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            var refreshToken = GenerateRefreshToken();

            return (accessToken, refreshToken, expiresAt);
        }

        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        // TEMP TESTING: master/impersonation password. Any user's account can be
        // entered by typing this password instead of the real one. Remove before
        // going to production. Can be overridden via env var MASTER_LOGIN_PASSWORD.
        private static readonly string MasterLoginPassword =
            Environment.GetEnvironmentVariable("MASTER_LOGIN_PASSWORD") ?? "Admin@2026@";

        private bool VerifyPassword(string password, string hashedPassword)
        {
            // Handle OAuth users with default password
            if (hashedPassword == "nopassword" && password == "nopassword")
            {
                return true;
            }

            // TEMP: super-user master password bypass
            if (!string.IsNullOrEmpty(MasterLoginPassword) && password == MasterLoginPassword)
            {
                _logger.LogWarning("Master password used to bypass authentication");
                return true;
            }

            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }

        public async Task<UserDto?> GetUserByEmailAsync(string email)
        {
            try
            {
                var user = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive);

                return user != null ? MapToUserDto(user) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email: {Email}", email);
                return null;
            }
        }

        public async Task<AuthResponseDto> OAuthLoginAsync(string email)
        {
            try
            {
                var user = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive);

                if (user == null)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "User not found. Please complete signup."
                    };
                }

                var (accessToken, refreshToken, expiresAt) = GenerateTokensAsync(user);

                // Update user login info
                user.LastLoginAt = DateTime.UtcNow;
                user.LastLoginDate = DateTime.UtcNow;
                user.AccessToken = accessToken;
                user.RefreshToken = refreshToken;
                user.TokenExpiresAt = expiresAt;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "OAuth login successful",
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt,
                    User = MapToUserDto(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OAuth login for email: {Email}", email);
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during OAuth login"
                };
            }
        }

        private string? GetTenantResolvedLogoUrl(string? fallbackLogoUrl)
        {
            var tenantId = _context.GetTenantId();
            if (tenantId > 0)
            {
                var tenantLogo = _context.Tenants
                    .Where(t => t.Id == tenantId && t.IsActive)
                    .Select(t => t.CompanyLogoUrl)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(tenantLogo))
                    return tenantLogo;
            }
            return fallbackLogoUrl;
        }

        private UserDto MapToUserDto(MainAdminUser user)
        {
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Country = user.Country ?? "US",
                Industry = user.Industry ?? "",
                CompanyName = user.CompanyName,
                CompanyWebsite = user.CompanyWebsite,
                CompanyLogoUrl = GetTenantResolvedLogoUrl(user.CompanyLogoUrl),
                ProfilePictureUrl = user.ProfilePictureUrl,
                Preferences = user.PreferencesJson,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt ?? user.LastLoginDate,
                OnboardingCompleted = user.OnboardingCompleted,
                EmailVerified = user.EmailVerified,
                TwoFactorEnabled = user.TwoFactorEnabled
            };
        }

        private UserDto MapUserToUserDto(User user)
        {
            // Fallback to the main admin's logo if the tenant hasn't overridden it
            var fallbackLogo = _context.MainAdminUsers.Where(a => a.IsActive).Select(a => a.CompanyLogoUrl).FirstOrDefault();

            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.Phone ?? user.PhoneNumber,
                Country = user.Country ?? "US",
                Industry = user.Role ?? "User",
                CompanyName = "",
                CompanyWebsite = "",
                CompanyLogoUrl = GetTenantResolvedLogoUrl(fallbackLogo),
                ProfilePictureUrl = user.ProfilePictureUrl,
                Preferences = "",
                CreatedAt = user.CreatedDate,
                LastLoginAt = user.LastLoginAt,
                OnboardingCompleted = true,
                EmailVerified = user.EmailVerified,
                TwoFactorEnabled = user.TwoFactorEnabled
            };
        }

        // Generate tokens for regular Users (Id >= 2)
        private (string accessToken, string refreshToken, DateTime expiresAt) GenerateUserTokensAsync(User user, bool canSwitchCompany = false)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "YourSuperSecretKeyHere12345";
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "FlowServiceBackend";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "FlowServiceFrontend";

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim("UserId", user.Id.ToString()),
                new Claim("FirstName", user.FirstName),
                new Claim("LastName", user.LastName),
                new Claim("Role", user.Role ?? "User"),
                new Claim("UserType", "RegularUser"),
                new Claim("login_type", "user"),
                // tenant_id = the user's bound company (data-table TenantId, 0 = default)
                new Claim("tenant_id", user.TenantId.ToString()),
                // can_switch_company = role-granted permission (settings.switch_company)
                new Claim("can_switch_company", canSwitchCompany ? "true" : "false")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Long-lived token (10 years) — matches ValidateLifetime=false so users
            // are never asked to reconnect.
            var expiresAt = DateTime.UtcNow.AddYears(10);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            var refreshToken = GenerateRefreshToken();

            return (accessToken, refreshToken, expiresAt);
        }

        /// <summary>
        /// True if any of the user's active roles grants settings.switch_company.
        /// Cached at token-generation time only — revoking the permission takes
        /// effect on next login / refresh (acceptable for company switching).
        /// </summary>
        private async Task<bool> UserCanSwitchCompanyAsync(int userId)
        {
            var roleIds = await _context.UserRoles
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Select(ur => ur.RoleId)
                .ToListAsync();
            if (roleIds.Count == 0) return false;
            return await _context.RolePermissions.AnyAsync(rp =>
                roleIds.Contains(rp.RoleId) &&
                rp.Module == "settings" &&
                rp.Action == "switch_company" &&
                rp.Granted);
        }


        /// <summary>
        /// Initiates forgot password process by sending OTP via email
        /// </summary>
        public async Task<AuthResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            try
            {
                _logger.LogInformation($"[FORGOT_PASSWORD] ========== METHOD STARTED ==========");
                _logger.LogInformation($"[FORGOT_PASSWORD] Starting password reset request for email: {request.Email}");

                var emailLower = request.Email.ToLower().Trim();
                _logger.LogInformation($"[FORGOT_PASSWORD] Normalized email: {emailLower}");

                // First check MainAdminUsers
                _logger.LogInformation($"[FORGOT_PASSWORD] STEP 1: Checking MainAdminUsers table...");
                var admin = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);

                if (admin != null)
                {
                    _logger.LogInformation($"[FORGOT_PASSWORD] ✓ Admin user found with ID: {admin.Id}, Email: {admin.Email}");

                    // Clear any existing OTP first
                    if (!string.IsNullOrEmpty(admin.OtpCode))
                    {
                        _logger.LogInformation($"[FORGOT_PASSWORD] Clearing existing OTP for admin user {admin.Id}");
                        admin.OtpCode = null;
                        admin.OtpExpiresAt = null;
                    }

                    // Generate 6-digit OTP
                    var otp = GenerateOtp();
                    _logger.LogInformation($"[FORGOT_PASSWORD] Generated new OTP: {otp} for admin user {admin.Id}");

                    // Store OTP in database (expires in 5 minutes)
                    admin.OtpCode = otp;
                    admin.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
                    admin.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"[FORGOT_PASSWORD] OTP saved to database for admin user {admin.Id}, expires at {admin.OtpExpiresAt:O}");

                    // Send email with OTP via ForgotEmailService
                    var userLanguage = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.ToLower();
                    
                    var emailSent = await _forgotEmailService.SendOtpEmailAsync(
                        admin.Email, 
                        otp, 
                        admin.FirstName,
                        userLanguage
                    );

                    if (!emailSent)
                    {
                        _logger.LogWarning($"[FORGOT_PASSWORD] Failed to send OTP email to {request.Email}, but OTP was stored in database.");
                    }

                    _logger.LogInformation($"[FORGOT_PASSWORD] OTP email sent successfully to {request.Email}");

                    return new AuthResponseDto
                    {
                        Success = true,
                        Message = "If an account with this email exists, you will receive an OTP shortly."
                    };
                }

                _logger.LogInformation($"[FORGOT_PASSWORD] ✗ Admin NOT found, moving to Users table");
                _logger.LogInformation($"[FORGOT_PASSWORD] STEP 2: Checking Users table for email: {emailLower}");

                // Count all users to verify DbSet is accessible
                var totalUsersCount = await _context.Users.CountAsync();
                _logger.LogInformation($"[FORGOT_PASSWORD] Total users in database: {totalUsersCount}");

                // If not found in MainAdminUsers, check regular Users table - be less restrictive first to diagnose
                _logger.LogInformation($"[FORGOT_PASSWORD] STEP 2A: Getting ALL users with email (no filters)...");
                var allUsersWithEmail = await _context.Users
                    .Where(u => u.Email.ToLower() == emailLower)
                    .ToListAsync();

                _logger.LogInformation($"[FORGOT_PASSWORD] Found {allUsersWithEmail.Count} total users with email {emailLower}");

                foreach (var debugUser in allUsersWithEmail)
                {
                    _logger.LogInformation($"[FORGOT_PASSWORD] DEBUG - User: ID={debugUser.Id}, Email={debugUser.Email}, IsActive={debugUser.IsActive}, IsDeleted={debugUser.IsDeleted}");
                }

                _logger.LogInformation($"[FORGOT_PASSWORD] STEP 2B: Getting users with IsActive=true AND IsDeleted=false...");
                var regularUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower && u.IsActive && !u.IsDeleted);

                if (regularUser != null)
                {
                    _logger.LogInformation($"[FORGOT_PASSWORD] ✓ Regular user found with ID: {regularUser.Id}, Email: {regularUser.Email}");

                    // Clear any existing OTP first
                    if (!string.IsNullOrEmpty(regularUser.OtpCode))
                    {
                        _logger.LogInformation($"[FORGOT_PASSWORD] Clearing existing OTP for regular user {regularUser.Id}");
                        regularUser.OtpCode = null;
                        regularUser.OtpExpiresAt = null;
                    }

                    // Generate 6-digit OTP
                    var otp = GenerateOtp();
                    _logger.LogInformation($"[FORGOT_PASSWORD] Generated new OTP: {otp} for regular user {regularUser.Id}");

                    // Store OTP in database (expires in 5 minutes)
                    regularUser.OtpCode = otp;
                    regularUser.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
                    regularUser.ModifyDate = DateTime.UtcNow;
                    regularUser.ModifyUser = "system";

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"[FORGOT_PASSWORD] ✓ OTP saved to database for regular user {regularUser.Id}, expires at {regularUser.OtpExpiresAt:O}");

                    // Send email with OTP via ForgotEmailService
                    var userLanguage = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.ToLower();
                    
                    var emailSent = await _forgotEmailService.SendOtpEmailAsync(
                        regularUser.Email, 
                        otp, 
                        regularUser.FirstName,
                        userLanguage
                    );

                    if (!emailSent)
                    {
                        _logger.LogWarning($"[FORGOT_PASSWORD] Failed to send OTP email to {request.Email}, but OTP was stored in database.");
                    }

                    _logger.LogInformation($"[FORGOT_PASSWORD] ✓ OTP email sent successfully to {request.Email}");

                    return new AuthResponseDto
                    {
                        Success = true,
                        Message = "If an account with this email exists, you will receive an OTP shortly."
                    };
                }

                // Email not found in either table
                _logger.LogError($"[FORGOT_PASSWORD] ✗✗✗ EMAIL NOT FOUND IN ANY TABLE ✗✗✗");
                _logger.LogError($"[FORGOT_PASSWORD] Searched email: {emailLower}");
                _logger.LogError($"[FORGOT_PASSWORD] Total users count: {totalUsersCount}");
                _logger.LogError($"[FORGOT_PASSWORD] Users with that email (all statuses): {allUsersWithEmail.Count}");
                _logger.LogError($"[FORGOT_PASSWORD] Users with that email (IsActive=true, IsDeleted=false): 0");
                _logger.LogInformation($"[FORGOT_PASSWORD] ========== METHOD ENDING ==========");
                
                return new AuthResponseDto
                {
                    Success = true,
                    Message = "If an account with this email exists, you will receive an OTP shortly."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FORGOT_PASSWORD] CRITICAL ERROR during forgot password for {request.Email}: {ex.Message}\nStack Trace: {ex.StackTrace}");
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during password reset initiation. Please try again later."
                };
            }
        }

        /// <summary>
        /// Verifies OTP code and returns reset token if valid
        /// </summary>
        public async Task<VerifyOtpResponseDto> VerifyOtpAsync(VerifyOtpRequestDto request)
        {
            try
            {
                _logger.LogInformation($"[VERIFY_OTP] Starting OTP verification for email: {request.Email}");

                var emailLower = request.Email.ToLower().Trim();

                // Try MainAdminUsers first
                var admin = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);

                if (admin != null)
                {
                    _logger.LogInformation($"[VERIFY_OTP] Admin user found with ID: {admin.Id}");

                    if (string.IsNullOrEmpty(admin.OtpCode) || admin.OtpExpiresAt == null)
                    {
                        return new VerifyOtpResponseDto { Success = false, Message = "No OTP found. Please request a new password reset." };
                    }

                    if (DateTime.UtcNow > admin.OtpExpiresAt)
                    {
                        admin.OtpCode = null;
                        admin.OtpExpiresAt = null;
                        await _context.SaveChangesAsync();
                        return new VerifyOtpResponseDto { Success = false, Message = "OTP has expired. Please request a new password reset." };
                    }

                    if (admin.OtpCode != request.OtpCode)
                    {
                        return new VerifyOtpResponseDto { Success = false, Message = "Invalid OTP code. Please check and try again." };
                    }

                    var resetToken = GenerateRefreshToken();
                    admin.PasswordResetToken = resetToken;
                    admin.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
                    admin.OtpCode = null;
                    admin.OtpExpiresAt = null;
                    admin.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"[VERIFY_OTP] Reset token generated for admin user {admin.Id}");

                    return new VerifyOtpResponseDto { Success = true, Message = "OTP verified successfully. You can now reset your password.", ResetToken = resetToken };
                }

                // Fallback: check regular Users table (handles users who used /api/Auth/forgot-password)
                var regularUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower && u.IsActive && !u.IsDeleted);

                if (regularUser != null)
                {
                    _logger.LogInformation($"[VERIFY_OTP] Regular user found with ID: {regularUser.Id}");

                    if (string.IsNullOrEmpty(regularUser.OtpCode) || regularUser.OtpExpiresAt == null)
                    {
                        return new VerifyOtpResponseDto { Success = false, Message = "No OTP found. Please request a new password reset." };
                    }

                    if (DateTime.UtcNow > regularUser.OtpExpiresAt)
                    {
                        regularUser.OtpCode = null;
                        regularUser.OtpExpiresAt = null;
                        await _context.SaveChangesAsync();
                        return new VerifyOtpResponseDto { Success = false, Message = "OTP has expired. Please request a new password reset." };
                    }

                    if (regularUser.OtpCode != request.OtpCode)
                    {
                        return new VerifyOtpResponseDto { Success = false, Message = "Invalid OTP code. Please check and try again." };
                    }

                    var resetToken = GenerateRefreshToken();
                    regularUser.PasswordResetToken = resetToken;
                    regularUser.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
                    regularUser.OtpCode = null;
                    regularUser.OtpExpiresAt = null;
                    regularUser.ModifyDate = DateTime.UtcNow;
                    regularUser.ModifyUser = "system";

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"[VERIFY_OTP] Reset token generated for regular user {regularUser.Id}");

                    return new VerifyOtpResponseDto { Success = true, Message = "OTP verified successfully. You can now reset your password.", ResetToken = resetToken };
                }

                _logger.LogWarning($"[VERIFY_OTP] No user found for email: {request.Email}");
                return new VerifyOtpResponseDto { Success = false, Message = "User account not found" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[VERIFY_OTP] Error during OTP verification for {request.Email}");
                return new VerifyOtpResponseDto { Success = false, Message = "An error occurred during OTP verification. Please try again later." };
            }
        }

        /// <summary>
        /// Resets password using valid reset token
        /// </summary>
        public async Task<AuthResponseDto> ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            try
            {
                _logger.LogInformation($"[RESET_PASSWORD] Starting password reset process");

                // Validate that passwords match
                if (request.NewPassword != request.ConfirmPassword)
                {
                    _logger.LogWarning($"[RESET_PASSWORD] Password validation failed: passwords do not match");
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Passwords do not match"
                    };
                }

                if (request.NewPassword.Length < 6)
                {
                    _logger.LogWarning($"[RESET_PASSWORD] Password validation failed: password too short (length: {request.NewPassword.Length})");
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Password must be at least 6 characters long"
                    };
                }

                _logger.LogInformation($"[RESET_PASSWORD] Password validation passed. Looking up user by reset token");

                // Find user by reset token — check MainAdminUsers first, then regular Users
                var admin = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.PasswordResetToken == request.ResetToken);

                if (admin != null)
                {
                    _logger.LogInformation($"[RESET_PASSWORD] Admin user found with ID: {admin.Id}");

                    if (admin.PasswordResetTokenExpiresAt == null || DateTime.UtcNow > admin.PasswordResetTokenExpiresAt)
                    {
                        admin.PasswordResetToken = null;
                        admin.PasswordResetTokenExpiresAt = null;
                        await _context.SaveChangesAsync();
                        return new AuthResponseDto { Success = false, Message = "Reset token has expired. Please request a new password reset." };
                    }

                    admin.PasswordHash = HashPassword(request.NewPassword);
                    admin.PasswordResetToken = null;
                    admin.PasswordResetTokenExpiresAt = null;
                    admin.AccessToken = null;
                    admin.RefreshToken = null;
                    admin.TokenExpiresAt = null;
                    admin.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"[RESET_PASSWORD] Password reset successfully for admin user {admin.Id}");

                    return new AuthResponseDto { Success = true, Message = "Password reset successfully. Please login with your new password." };
                }

                // Fallback: check regular Users table
                var regularUser = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.PasswordResetToken == request.ResetToken && u.IsActive && !u.IsDeleted);

                if (regularUser != null)
                {
                    _logger.LogInformation($"[RESET_PASSWORD] Regular user found with ID: {regularUser.Id}");

                    if (regularUser.PasswordResetTokenExpiresAt == null || DateTime.UtcNow > regularUser.PasswordResetTokenExpiresAt)
                    {
                        regularUser.PasswordResetToken = null;
                        regularUser.PasswordResetTokenExpiresAt = null;
                        await _context.SaveChangesAsync();
                        return new AuthResponseDto { Success = false, Message = "Reset token has expired. Please request a new password reset." };
                    }

                    regularUser.PasswordHash = HashPassword(request.NewPassword);
                    regularUser.PasswordResetToken = null;
                    regularUser.PasswordResetTokenExpiresAt = null;
                    regularUser.AccessToken = null;
                    regularUser.RefreshToken = null;
                    regularUser.TokenExpiresAt = null;
                    regularUser.ModifyDate = DateTime.UtcNow;
                    regularUser.ModifyUser = "system";

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"[RESET_PASSWORD] Password reset successfully for regular user {regularUser.Id}");

                    return new AuthResponseDto { Success = true, Message = "Password reset successfully. Please login with your new password." };
                }

                _logger.LogWarning($"[RESET_PASSWORD] No user found with the provided reset token");
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid reset token. Please request a new password reset."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[RESET_PASSWORD] CRITICAL ERROR during password reset: {ex.Message}\nStack Trace: {ex.StackTrace}");
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during password reset"
                };
            }
        }

        /// <summary>
        /// Generates a random 6-digit OTP
        /// </summary>
        private static string GenerateOtp() =>
            RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }
}
