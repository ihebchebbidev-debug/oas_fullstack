using MyApi.Modules.Settings.DTOs;

namespace MyApi.Modules.Settings.Services
{
    public interface IAppSettingsService
    {
        Task<string?> GetSettingAsync(string key);
        Task<AppSettingDto> SetSettingAsync(string key, string value);
        Task<List<AppSettingDto>> GetAllSettingsAsync();
    }
}
