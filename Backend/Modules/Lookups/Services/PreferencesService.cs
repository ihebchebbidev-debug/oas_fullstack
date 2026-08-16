using MyApi.Data;
using MyApi.Modules.Lookups.DTOs;
using MyApi.Modules.Users.Models;
using MyApi.Modules.Auth.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MyApi.Modules.Lookups.Services
{
    public class PreferencesService : IPreferencesService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PreferencesService> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public PreferencesService(ApplicationDbContext context, ILogger<PreferencesService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get preferences - checks MainAdminUsers first (PreferencesJson column), 
        /// then UserPreferences table for regular users
        /// </summary>
        public async Task<PreferencesResponse?> GetUserPreferencesAsync(string userId)
        {
            try
            {
                if (!int.TryParse(userId, out int userIdInt))
                    return null;

                // First check if this is a MainAdminUser
                var adminUser = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Id == userIdInt);

                if (adminUser != null)
                {
                    // Return preferences from MainAdminUsers.PreferencesJson
                    return new PreferencesResponse
                    {
                        Id = adminUser.Id,
                        UserId = adminUser.Id,
                        PreferencesJson = adminUser.PreferencesJson ?? "{}",
                        UpdatedAt = adminUser.UpdatedAt ?? adminUser.CreatedAt,
                        Preferences = ParsePreferencesJson(adminUser.PreferencesJson ?? "{}")
                    };
                }

                // Check UserPreferences table for regular users
                var preferences = await _context.UserPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userIdInt);

                if (preferences == null)
                    return null;

                return MapToResponse(preferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user preferences for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Create preferences - for MainAdminUsers updates PreferencesJson column,
        /// for regular users creates UserPreferences record
        /// </summary>
        public async Task<PreferencesResponse> CreateUserPreferencesAsync(string userId, CreatePreferencesRequest request)
        {
            try
            {
                if (!int.TryParse(userId, out int userIdInt))
                    throw new ArgumentException("Invalid user ID format");

                var preferencesJson = request.ToJsonString();

                // First check if this is a MainAdminUser
                var adminUser = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Id == userIdInt);

                if (adminUser != null)
                {
                    // Update MainAdminUsers.PreferencesJson directly
                    adminUser.PreferencesJson = preferencesJson;
                    adminUser.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return new PreferencesResponse
                    {
                        Id = adminUser.Id,
                        UserId = adminUser.Id,
                        PreferencesJson = preferencesJson,
                        UpdatedAt = adminUser.UpdatedAt ?? DateTime.UtcNow,
                        Preferences = ParsePreferencesJson(preferencesJson)
                    };
                }

                // For regular users, use UserPreferences table
                var existingPreferences = await _context.UserPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userIdInt);

                if (existingPreferences != null)
                {
                    throw new InvalidOperationException("User preferences already exist. Use update instead.");
                }

                var preferences = new UserPreferences
                {
                    UserId = userIdInt,
                    PreferencesJson = preferencesJson,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.UserPreferences.Add(preferences);
                await _context.SaveChangesAsync();

                return MapToResponse(preferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user preferences for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Update preferences - for MainAdminUsers updates PreferencesJson column,
        /// for regular users updates UserPreferences record
        /// </summary>
        public async Task<PreferencesResponse?> UpdateUserPreferencesAsync(string userId, UpdatePreferencesRequest request)
        {
            try
            {
                if (!int.TryParse(userId, out int userIdInt))
                    return null;

                // First check if this is a MainAdminUser
                var adminUser = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Id == userIdInt);

                if (adminUser != null)
                {
                    string newPreferencesJson;
                    
                    if (!string.IsNullOrEmpty(request.PreferencesJson))
                    {
                        newPreferencesJson = request.PreferencesJson;
                    }
                    else
                    {
                        var existing = ParsePreferencesJson(adminUser.PreferencesJson ?? "{}");
                        var merged = request.MergeWith(existing);
                        newPreferencesJson = JsonSerializer.Serialize(merged, JsonOptions);
                    }

                    adminUser.PreferencesJson = newPreferencesJson;
                    adminUser.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return new PreferencesResponse
                    {
                        Id = adminUser.Id,
                        UserId = adminUser.Id,
                        PreferencesJson = newPreferencesJson,
                        UpdatedAt = adminUser.UpdatedAt ?? DateTime.UtcNow,
                        Preferences = ParsePreferencesJson(newPreferencesJson)
                    };
                }

                // For regular users, use UserPreferences table
                var preferences = await _context.UserPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userIdInt);

                if (preferences == null)
                    return null;

                if (!string.IsNullOrEmpty(request.PreferencesJson))
                {
                    preferences.PreferencesJson = request.PreferencesJson;
                }
                else
                {
                    var existing = ParsePreferencesJson(preferences.PreferencesJson);
                    var merged = request.MergeWith(existing);
                    preferences.PreferencesJson = JsonSerializer.Serialize(merged, JsonOptions);
                }

                preferences.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return MapToResponse(preferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user preferences for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Delete preferences - for MainAdminUsers clears PreferencesJson,
        /// for regular users deletes UserPreferences record
        /// </summary>
        public async Task<bool> DeleteUserPreferencesAsync(string userId)
        {
            try
            {
                if (!int.TryParse(userId, out int userIdInt))
                    return false;

                // First check if this is a MainAdminUser
                var adminUser = await _context.MainAdminUsers
                    .FirstOrDefaultAsync(u => u.Id == userIdInt);

                if (adminUser != null)
                {
                    adminUser.PreferencesJson = "{}";
                    adminUser.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return true;
                }

                // For regular users, delete from UserPreferences table
                var preferences = await _context.UserPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userIdInt);

                if (preferences == null)
                    return false;

                _context.UserPreferences.Remove(preferences);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user preferences for user {UserId}", userId);
                throw;
            }
        }

        private PreferencesResponse MapToResponse(UserPreferences preferences)
        {
            var parsed = ParsePreferencesJson(preferences.PreferencesJson);
            
            return new PreferencesResponse
            {
                Id = preferences.Id,
                UserId = preferences.UserId,
                PreferencesJson = preferences.PreferencesJson,
                UpdatedAt = preferences.UpdatedAt,
                Preferences = parsed
            };
        }

        private PreferencesData ParsePreferencesJson(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json) || json == "{}")
                    return new PreferencesData();

                return JsonSerializer.Deserialize<PreferencesData>(json, JsonOptions) ?? new PreferencesData();
            }
            catch
            {
                return new PreferencesData();
            }
        }
    }
}
