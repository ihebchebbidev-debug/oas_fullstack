using MyApi.Modules.UserAiSettings.DTOs;

namespace MyApi.Modules.UserAiSettings.Services
{
    public interface IUserAiSettingsService
    {
        // Keys
        Task<List<UserAiKeyDto>> GetKeysAsync(int userId, string userType);
        Task<UserAiKeyDto> AddKeyAsync(int userId, string userType, CreateUserAiKeyDto dto);
        Task<UserAiKeyDto?> UpdateKeyAsync(int userId, string userType, int keyId, UpdateUserAiKeyDto dto);
        Task<bool> DeleteKeyAsync(int userId, string userType, int keyId);
        Task<bool> ReorderKeysAsync(int userId, string userType, List<int> keyIds);

        // Preferences
        Task<UserAiPreferencesDto?> GetPreferencesAsync(int userId, string userType);
        Task<UserAiPreferencesDto> UpdatePreferencesAsync(int userId, string userType, UpdateUserAiPreferencesDto dto);
    }
}
