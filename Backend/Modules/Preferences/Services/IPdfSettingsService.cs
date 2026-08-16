using MyApi.Modules.Preferences.DTOs;

namespace MyApi.Modules.Preferences.Services
{
    public interface IPdfSettingsService
    {
        Task<List<PdfSettingsDto>> GetAllSettingsAsync();
        Task<PdfSettingsDto?> GetSettingsByModuleAsync(string module);
        Task<PdfSettingsDto> CreateOrUpdateSettingsAsync(string module, object? settingsJson);
        Task<bool> DeleteSettingsAsync(string module);
    }
}
