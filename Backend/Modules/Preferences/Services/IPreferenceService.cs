using MyApi.Modules.Preferences.DTOs;

namespace MyApi.Modules.Preferences.Services
{
    public interface IPreferenceService
    {
        Task<UserPreferenceDto?> GetByUserIdAsync(int userId);
        Task<UserPreferenceDto> CreateAsync(int userId, CreatePreferenceDto dto);
        Task<UserPreferenceDto?> UpdateAsync(int userId, UpdatePreferenceDto dto);
        Task<bool> DeleteAsync(int userId);
    }
}