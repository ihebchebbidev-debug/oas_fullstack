using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.UserAiSettings.DTOs;
using MyApi.Modules.UserAiSettings.Models;

namespace MyApi.Modules.UserAiSettings.Services
{
    public class UserAiSettingsService : IUserAiSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserAiSettingsService> _logger;

        public UserAiSettingsService(ApplicationDbContext context, ILogger<UserAiSettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ─── Keys ───

        public async Task<List<UserAiKeyDto>> GetKeysAsync(int userId, string userType)
        {
            var keys = await _context.UserAiKeys
                .Where(k => k.UserId == userId && k.UserType == userType && k.IsActive)
                .OrderBy(k => k.Priority)
                .ToListAsync();

            return keys.Select(k => MapKeyToDto(k, mask: true)).ToList();
        }

        public async Task<UserAiKeyDto> AddKeyAsync(int userId, string userType, CreateUserAiKeyDto dto)
        {
            // Auto-assign next priority
            var maxPriority = await _context.UserAiKeys
                .Where(k => k.UserId == userId && k.UserType == userType && k.IsActive)
                .MaxAsync(k => (int?)k.Priority) ?? -1;

            var key = new UserAiKey
            {
                UserId = userId,
                UserType = userType,
                Label = dto.Label,
                ApiKey = dto.ApiKey,
                Provider = dto.Provider,
                Priority = dto.Priority > 0 ? dto.Priority : maxPriority + 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserAiKeys.Add(key);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} ({UserType}) added AI key '{Label}' (provider: {Provider})",
                userId, userType, dto.Label, dto.Provider);

            return MapKeyToDto(key, mask: true);
        }

        public async Task<UserAiKeyDto?> UpdateKeyAsync(int userId, string userType, int keyId, UpdateUserAiKeyDto dto)
        {
            var key = await _context.UserAiKeys
                .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId && k.UserType == userType);

            if (key == null) return null;

            if (dto.Label != null) key.Label = dto.Label;
            if (dto.ApiKey != null) key.ApiKey = dto.ApiKey;
            if (dto.Priority.HasValue) key.Priority = dto.Priority.Value;
            if (dto.IsActive.HasValue) key.IsActive = dto.IsActive.Value;
            key.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapKeyToDto(key, mask: true);
        }

        public async Task<bool> DeleteKeyAsync(int userId, string userType, int keyId)
        {
            var key = await _context.UserAiKeys
                .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId && k.UserType == userType);

            if (key == null) return false;

            _context.UserAiKeys.Remove(key);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} ({UserType}) deleted AI key {KeyId}", userId, userType, keyId);
            return true;
        }

        public async Task<bool> ReorderKeysAsync(int userId, string userType, List<int> keyIds)
        {
            var keys = await _context.UserAiKeys
                .Where(k => k.UserId == userId && k.UserType == userType && keyIds.Contains(k.Id))
                .ToListAsync();

            for (int i = 0; i < keyIds.Count; i++)
            {
                var key = keys.FirstOrDefault(k => k.Id == keyIds[i]);
                if (key != null)
                {
                    key.Priority = i;
                    key.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // ─── Preferences ───

        public async Task<UserAiPreferencesDto?> GetPreferencesAsync(int userId, string userType)
        {
            var pref = await _context.UserAiPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId && p.UserType == userType);

            return pref != null ? MapPrefToDto(pref) : null;
        }

        public async Task<UserAiPreferencesDto> UpdatePreferencesAsync(int userId, string userType, UpdateUserAiPreferencesDto dto)
        {
            var pref = await _context.UserAiPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId && p.UserType == userType);

            if (pref == null)
            {
                pref = new UserAiPreference
                {
                    UserId = userId,
                    UserType = userType,
                    DefaultModel = dto.DefaultModel,
                    FallbackModel = dto.FallbackModel,
                    DefaultTemperature = dto.DefaultTemperature ?? 0.70m,
                    DefaultMaxTokens = dto.DefaultMaxTokens ?? 1000,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserAiPreferences.Add(pref);
            }
            else
            {
                if (dto.DefaultModel != null) pref.DefaultModel = dto.DefaultModel;
                if (dto.FallbackModel != null) pref.FallbackModel = dto.FallbackModel;
                if (dto.DefaultTemperature.HasValue) pref.DefaultTemperature = dto.DefaultTemperature.Value;
                if (dto.DefaultMaxTokens.HasValue) pref.DefaultMaxTokens = dto.DefaultMaxTokens.Value;
                pref.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return MapPrefToDto(pref);
        }

        // ─── Mappers ───

        private static UserAiKeyDto MapKeyToDto(UserAiKey key, bool mask)
        {
            return new UserAiKeyDto
            {
                Id = key.Id,
                UserId = key.UserId,
                Label = key.Label,
                ApiKey = mask ? MaskApiKey(key.ApiKey) : key.ApiKey,
                Provider = key.Provider,
                Priority = key.Priority,
                IsActive = key.IsActive,
                CreatedAt = key.CreatedAt,
                UpdatedAt = key.UpdatedAt
            };
        }

        private static string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 12)
                return "••••••••";

            return apiKey[..8] + new string('•', Math.Max(0, apiKey.Length - 12)) + apiKey[^4..];
        }

        private static UserAiPreferencesDto MapPrefToDto(UserAiPreference pref)
        {
            return new UserAiPreferencesDto
            {
                Id = pref.Id,
                UserId = pref.UserId,
                DefaultModel = pref.DefaultModel,
                FallbackModel = pref.FallbackModel,
                DefaultTemperature = pref.DefaultTemperature,
                DefaultMaxTokens = pref.DefaultMaxTokens,
                UpdatedAt = pref.UpdatedAt
            };
        }
    }
}
