using MyApi.Data;
using MyApi.Modules.Preferences.DTOs;
using MyApi.Modules.Preferences.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MyApi.Modules.Preferences.Services
{
    public class PdfSettingsService : IPdfSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PdfSettingsService> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public PdfSettingsService(ApplicationDbContext context, ILogger<PdfSettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<PdfSettingsDto>> GetAllSettingsAsync()
        {
            try
            {
                var settings = await _context.Set<PdfSettings>().ToListAsync();
                return settings.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all PDF settings");
                throw;
            }
        }

        public async Task<PdfSettingsDto?> GetSettingsByModuleAsync(string module)
        {
            try
            {
                var settings = await _context.Set<PdfSettings>()
                    .FirstOrDefaultAsync(s => s.Module == module);

                if (settings == null)
                    return null;

                return MapToDto(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting PDF settings for module {Module}", module);
                throw;
            }
        }

        public async Task<PdfSettingsDto> CreateOrUpdateSettingsAsync(string module, object? settingsJson)
        {
            try
            {
                var settings = await _context.Set<PdfSettings>()
                    .FirstOrDefaultAsync(s => s.Module == module);

                string jsonString = settingsJson != null
                    ? JsonSerializer.Serialize(settingsJson, JsonOptions)
                    : "{}";

                if (settings == null)
                {
                    // Create new
                    settings = new PdfSettings
                    {
                        Module = module,
                        SettingsJson = jsonString,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Set<PdfSettings>().Add(settings);
                }
                else
                {
                    // Update existing
                    settings.SettingsJson = jsonString;
                    settings.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return MapToDto(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating/updating PDF settings for module {Module}", module);
                throw;
            }
        }

        public async Task<bool> DeleteSettingsAsync(string module)
        {
            try
            {
                var settings = await _context.Set<PdfSettings>()
                    .FirstOrDefaultAsync(s => s.Module == module);

                if (settings == null)
                    return false;

                _context.Set<PdfSettings>().Remove(settings);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting PDF settings for module {Module}", module);
                throw;
            }
        }

        private PdfSettingsDto MapToDto(PdfSettings settings)
        {
            object? parsedJson = null;
            try
            {
                if (!string.IsNullOrEmpty(settings.SettingsJson) && settings.SettingsJson != "{}")
                {
                    parsedJson = JsonSerializer.Deserialize<object>(settings.SettingsJson, JsonOptions);
                }
            }
            catch
            {
                parsedJson = settings.SettingsJson;
            }

            return new PdfSettingsDto
            {
                Id = settings.Id,
                Module = settings.Module,
                SettingsJson = parsedJson,
                UpdatedAt = settings.UpdatedAt
            };
        }
    }
}
