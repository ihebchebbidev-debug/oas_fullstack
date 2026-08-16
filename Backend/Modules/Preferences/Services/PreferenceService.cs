using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Preferences.DTOs;
using MyApi.Modules.Preferences.Models;

namespace MyApi.Modules.Preferences.Services
{
    public class PreferenceService : IPreferenceService
    {
        private readonly ApplicationDbContext _context;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public PreferenceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UserPreferenceDto?> GetByUserIdAsync(int userId)
        {
            var preference = await _context.Set<UserPreference>()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            return preference != null ? MapToDto(preference) : null;
        }

        public async Task<UserPreferenceDto> CreateAsync(int userId, CreatePreferenceDto dto)
        {
            // Check if preference already exists
            var existing = await _context.Set<UserPreference>()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (existing != null)
            {
                // Update existing instead
                return await UpdateExisting(existing, dto);
            }

            var preferencesData = new PreferencesData
            {
                Theme = dto.Theme,
                Language = dto.Language,
                PrimaryColor = dto.PrimaryColor,
                LayoutMode = dto.LayoutMode,
                DataView = dto.DataView,
                Timezone = dto.Timezone ?? "UTC",
                DateFormat = dto.DateFormat ?? "MM/DD/YYYY",
                TimeFormat = dto.TimeFormat ?? "12h",
                Currency = dto.Currency ?? "TND",
                NumberFormat = dto.NumberFormat,
                Notifications = dto.Notifications,
                SidebarCollapsed = dto.SidebarCollapsed ?? false,
                CompactMode = dto.CompactMode ?? false,
                ShowTooltips = dto.ShowTooltips ?? true,
                AnimationsEnabled = dto.AnimationsEnabled ?? true,
                SoundEnabled = dto.SoundEnabled ?? false,
                AutoSave = dto.AutoSave ?? true,
                WorkArea = dto.WorkArea,
                DashboardLayout = dto.DashboardLayout,
                QuickAccessItems = dto.QuickAccessItems
            };

            var preference = new UserPreference
            {
                UserId = userId,
                PreferencesJson = JsonSerializer.Serialize(preferencesData, _jsonOptions),
                UpdatedAt = DateTime.UtcNow
            };

            _context.Set<UserPreference>().Add(preference);
            await _context.SaveChangesAsync();

            return MapToDto(preference);
        }

        private async Task<UserPreferenceDto> UpdateExisting(UserPreference preference, CreatePreferenceDto dto)
        {
            var existingData = ParsePreferencesJson(preference.PreferencesJson);
            
            existingData.Theme = dto.Theme;
            existingData.Language = dto.Language;
            existingData.PrimaryColor = dto.PrimaryColor;
            existingData.LayoutMode = dto.LayoutMode;
            existingData.DataView = dto.DataView;
            if (dto.Timezone != null) existingData.Timezone = dto.Timezone;
            if (dto.DateFormat != null) existingData.DateFormat = dto.DateFormat;
            if (dto.TimeFormat != null) existingData.TimeFormat = dto.TimeFormat;
            if (dto.Currency != null) existingData.Currency = dto.Currency;
            if (dto.NumberFormat != null) existingData.NumberFormat = dto.NumberFormat;
            if (dto.Notifications != null) existingData.Notifications = dto.Notifications;
            if (dto.SidebarCollapsed.HasValue) existingData.SidebarCollapsed = dto.SidebarCollapsed.Value;
            if (dto.CompactMode.HasValue) existingData.CompactMode = dto.CompactMode.Value;
            if (dto.ShowTooltips.HasValue) existingData.ShowTooltips = dto.ShowTooltips.Value;
            if (dto.AnimationsEnabled.HasValue) existingData.AnimationsEnabled = dto.AnimationsEnabled.Value;
            if (dto.SoundEnabled.HasValue) existingData.SoundEnabled = dto.SoundEnabled.Value;
            if (dto.AutoSave.HasValue) existingData.AutoSave = dto.AutoSave.Value;
            if (dto.WorkArea != null) existingData.WorkArea = dto.WorkArea;
            if (dto.DashboardLayout != null) existingData.DashboardLayout = dto.DashboardLayout;
            if (dto.QuickAccessItems != null) existingData.QuickAccessItems = dto.QuickAccessItems;

            preference.PreferencesJson = JsonSerializer.Serialize(existingData, _jsonOptions);
            preference.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapToDto(preference);
        }

        public async Task<UserPreferenceDto?> UpdateAsync(int userId, UpdatePreferenceDto dto)
        {
            var preference = await _context.Set<UserPreference>()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (preference == null)
            {
                // Create new if doesn't exist
                var createDto = new CreatePreferenceDto
                {
                    Theme = dto.Theme ?? "system",
                    Language = dto.Language ?? "en",
                    PrimaryColor = dto.PrimaryColor ?? "blue",
                    LayoutMode = dto.LayoutMode ?? "sidebar",
                    DataView = dto.DataView ?? "table",
                    Timezone = dto.Timezone,
                    DateFormat = dto.DateFormat,
                    TimeFormat = dto.TimeFormat,
                    Currency = dto.Currency,
                    NumberFormat = dto.NumberFormat,
                    Notifications = dto.Notifications,
                    SidebarCollapsed = dto.SidebarCollapsed,
                    CompactMode = dto.CompactMode,
                    ShowTooltips = dto.ShowTooltips,
                    AnimationsEnabled = dto.AnimationsEnabled,
                    SoundEnabled = dto.SoundEnabled,
                    AutoSave = dto.AutoSave,
                    WorkArea = dto.WorkArea,
                    DashboardLayout = dto.DashboardLayout,
                    QuickAccessItems = dto.QuickAccessItems
                };
                return await CreateAsync(userId, createDto);
            }

            var existingData = ParsePreferencesJson(preference.PreferencesJson);

            // Update only provided fields
            if (dto.Theme != null) existingData.Theme = dto.Theme;
            if (dto.Language != null) existingData.Language = dto.Language;
            if (dto.PrimaryColor != null) existingData.PrimaryColor = dto.PrimaryColor;
            if (dto.LayoutMode != null) existingData.LayoutMode = dto.LayoutMode;
            if (dto.DataView != null) existingData.DataView = dto.DataView;
            if (dto.Timezone != null) existingData.Timezone = dto.Timezone;
            if (dto.DateFormat != null) existingData.DateFormat = dto.DateFormat;
            if (dto.TimeFormat != null) existingData.TimeFormat = dto.TimeFormat;
            if (dto.Currency != null) existingData.Currency = dto.Currency;
            if (dto.NumberFormat != null) existingData.NumberFormat = dto.NumberFormat;
            if (dto.Notifications != null) existingData.Notifications = dto.Notifications;
            if (dto.SidebarCollapsed.HasValue) existingData.SidebarCollapsed = dto.SidebarCollapsed.Value;
            if (dto.CompactMode.HasValue) existingData.CompactMode = dto.CompactMode.Value;
            if (dto.ShowTooltips.HasValue) existingData.ShowTooltips = dto.ShowTooltips.Value;
            if (dto.AnimationsEnabled.HasValue) existingData.AnimationsEnabled = dto.AnimationsEnabled.Value;
            if (dto.SoundEnabled.HasValue) existingData.SoundEnabled = dto.SoundEnabled.Value;
            if (dto.AutoSave.HasValue) existingData.AutoSave = dto.AutoSave.Value;
            if (dto.WorkArea != null) existingData.WorkArea = dto.WorkArea;
            if (dto.DashboardLayout != null) existingData.DashboardLayout = dto.DashboardLayout;
            if (dto.QuickAccessItems != null) existingData.QuickAccessItems = dto.QuickAccessItems;
            
            preference.PreferencesJson = JsonSerializer.Serialize(existingData, _jsonOptions);
            preference.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapToDto(preference);
        }

        public async Task<bool> DeleteAsync(int userId)
        {
            var preference = await _context.Set<UserPreference>()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (preference == null)
                return false;

            _context.Set<UserPreference>().Remove(preference);
            await _context.SaveChangesAsync();
            return true;
        }

        private PreferencesData ParsePreferencesJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<PreferencesData>(json, _jsonOptions) ?? new PreferencesData();
            }
            catch
            {
                return new PreferencesData();
            }
        }

        private UserPreferenceDto MapToDto(UserPreference p)
        {
            var data = ParsePreferencesJson(p.PreferencesJson);
            
            return new UserPreferenceDto
            {
                Id = p.Id,
                UserId = p.UserId,
                Theme = data.Theme,
                Language = data.Language,
                PrimaryColor = data.PrimaryColor,
                LayoutMode = data.LayoutMode,
                DataView = data.DataView,
                Timezone = data.Timezone,
                DateFormat = data.DateFormat,
                TimeFormat = data.TimeFormat,
                Currency = data.Currency,
                NumberFormat = data.NumberFormat,
                Notifications = data.Notifications,
                SidebarCollapsed = data.SidebarCollapsed,
                CompactMode = data.CompactMode,
                ShowTooltips = data.ShowTooltips,
                AnimationsEnabled = data.AnimationsEnabled,
                SoundEnabled = data.SoundEnabled,
                AutoSave = data.AutoSave,
                WorkArea = data.WorkArea,
                DashboardLayout = data.DashboardLayout,
                QuickAccessItems = data.QuickAccessItems,
                UpdatedAt = p.UpdatedAt
            };
        }
    }
}