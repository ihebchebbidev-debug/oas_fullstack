using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Settings.DTOs;
using MyApi.Modules.Settings.Models;

namespace MyApi.Modules.Settings.Services
{
    public class AppSettingsService : IAppSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AppSettingsService> _logger;

        // Default values for known settings
        private static readonly Dictionary<string, string> Defaults = new()
        {
            { "JobConversionMode", "installation" }
        };

        public AppSettingsService(ApplicationDbContext context, ILogger<AppSettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<string?> GetSettingAsync(string key)
        {
            try
            {
                var setting = await _context.Set<AppSettings>()
                    .FirstOrDefaultAsync(s => s.SettingKey == key);

                if (setting != null)
                    return setting.Value;

                // Return default if known
                return Defaults.TryGetValue(key, out var defaultValue) ? defaultValue : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting app setting {Key}", key);
                // Return default on error to avoid breaking callers
                return Defaults.TryGetValue(key, out var defaultValue) ? defaultValue : null;
            }
        }

        public async Task<AppSettingDto> SetSettingAsync(string key, string value)
        {
            try
            {
                var setting = await _context.Set<AppSettings>()
                    .FirstOrDefaultAsync(s => s.SettingKey == key);

                if (setting == null)
                {
                    setting = new AppSettings
                    {
                        SettingKey = key,
                        Value = value,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Set<AppSettings>().Add(setting);
                }
                else
                {
                    setting.Value = value;
                    setting.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return new AppSettingDto
                {
                    Key = setting.SettingKey,
                    Value = setting.Value,
                    UpdatedAt = setting.UpdatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting app setting {Key}", key);
                throw;
            }
        }

        public async Task<List<AppSettingDto>> GetAllSettingsAsync()
        {
            try
            {
                var settings = await _context.Set<AppSettings>().ToListAsync();
                return settings.Select(s => new AppSettingDto
                {
                    Key = s.SettingKey,
                    Value = s.Value,
                    UpdatedAt = s.UpdatedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all app settings");
                throw;
            }
        }
    }
}
