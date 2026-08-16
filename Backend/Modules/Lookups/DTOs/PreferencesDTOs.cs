using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MyApi.Modules.Lookups.DTOs
{
    // Simple DTO matching database structure
    public class UserPreferencesDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string PreferencesJson { get; set; } = "{}";
        public DateTime UpdatedAt { get; set; }
    }

    // Expanded preferences object (parsed from JSON)
    public class PreferencesData
    {
        public string Theme { get; set; } = "system";
        public string Language { get; set; } = "en";
        public string PrimaryColor { get; set; } = "blue";
        public string LayoutMode { get; set; } = "sidebar";
        public string DataView { get; set; } = "table";
        public string? Timezone { get; set; }
        public string DateFormat { get; set; } = "MM/DD/YYYY";
        public string TimeFormat { get; set; } = "12h";
        public string Currency { get; set; } = "TND";
        public string NumberFormat { get; set; } = "comma";
        public string? Notifications { get; set; } = "{}";
        public bool SidebarCollapsed { get; set; } = false;
        public bool CompactMode { get; set; } = false;
        public bool ShowTooltips { get; set; } = true;
        public bool AnimationsEnabled { get; set; } = true;
        public bool SoundEnabled { get; set; } = true;
        public bool AutoSave { get; set; } = true;
        public string? WorkArea { get; set; }
        public string? DashboardLayout { get; set; }
        public string? QuickAccessItems { get; set; } = "[]";
    }

    // Response DTO with both raw and parsed data
    public class PreferencesResponse
    {
        public required int Id { get; set; }
        public required int UserId { get; set; }
        public required string PreferencesJson { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Parsed preferences (convenience)
        public PreferencesData? Preferences { get; set; }
    }

    // Create request - accepts either JSON string or expanded object
    public class CreatePreferencesRequest
    {
        public string? PreferencesJson { get; set; }
        
        // Or individual fields (will be serialized to JSON)
        public string Theme { get; set; } = "system";
        public string Language { get; set; } = "en";
        public string PrimaryColor { get; set; } = "blue";
        public string LayoutMode { get; set; } = "sidebar";
        public string DataView { get; set; } = "table";
        public string? Timezone { get; set; }
        public string DateFormat { get; set; } = "MM/DD/YYYY";
        public string TimeFormat { get; set; } = "12h";
        public string Currency { get; set; } = "TND";
        public string NumberFormat { get; set; } = "comma";
        public string? Notifications { get; set; } = "{}";
        public bool? SidebarCollapsed { get; set; }
        public bool? CompactMode { get; set; }
        public bool? ShowTooltips { get; set; }
        public bool? AnimationsEnabled { get; set; }
        public bool? SoundEnabled { get; set; }
        public bool? AutoSave { get; set; }
        public string? WorkArea { get; set; }
        public string? DashboardLayout { get; set; }
        public string? QuickAccessItems { get; set; }

        public string ToJsonString()
        {
            if (!string.IsNullOrEmpty(PreferencesJson))
                return PreferencesJson;

            var data = new PreferencesData
            {
                Theme = Theme,
                Language = Language,
                PrimaryColor = PrimaryColor,
                LayoutMode = LayoutMode,
                DataView = DataView,
                Timezone = Timezone,
                DateFormat = DateFormat,
                TimeFormat = TimeFormat,
                Currency = Currency,
                NumberFormat = NumberFormat,
                Notifications = Notifications,
                SidebarCollapsed = SidebarCollapsed ?? false,
                CompactMode = CompactMode ?? false,
                ShowTooltips = ShowTooltips ?? true,
                AnimationsEnabled = AnimationsEnabled ?? true,
                SoundEnabled = SoundEnabled ?? true,
                AutoSave = AutoSave ?? true,
                WorkArea = WorkArea,
                DashboardLayout = DashboardLayout,
                QuickAccessItems = QuickAccessItems
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
        }
    }

    public class UpdatePreferencesRequest
    {
        public string? PreferencesJson { get; set; }
        
        // Or individual fields
        public string? Theme { get; set; }
        public string? Language { get; set; }
        public string? PrimaryColor { get; set; }
        public string? LayoutMode { get; set; }
        public string? DataView { get; set; }
        public string? Timezone { get; set; }
        public string? DateFormat { get; set; }
        public string? TimeFormat { get; set; }
        public string? Currency { get; set; }
        public string? NumberFormat { get; set; }
        public string? Notifications { get; set; }
        public bool? SidebarCollapsed { get; set; }
        public bool? CompactMode { get; set; }
        public bool? ShowTooltips { get; set; }
        public bool? AnimationsEnabled { get; set; }
        public bool? SoundEnabled { get; set; }
        public bool? AutoSave { get; set; }
        public string? WorkArea { get; set; }
        public string? DashboardLayout { get; set; }
        public string? QuickAccessItems { get; set; }

        public PreferencesData MergeWith(PreferencesData existing)
        {
            return new PreferencesData
            {
                Theme = Theme ?? existing.Theme,
                Language = Language ?? existing.Language,
                PrimaryColor = PrimaryColor ?? existing.PrimaryColor,
                LayoutMode = LayoutMode ?? existing.LayoutMode,
                DataView = DataView ?? existing.DataView,
                Timezone = Timezone ?? existing.Timezone,
                DateFormat = DateFormat ?? existing.DateFormat,
                TimeFormat = TimeFormat ?? existing.TimeFormat,
                Currency = Currency ?? existing.Currency,
                NumberFormat = NumberFormat ?? existing.NumberFormat,
                Notifications = Notifications ?? existing.Notifications,
                SidebarCollapsed = SidebarCollapsed ?? existing.SidebarCollapsed,
                CompactMode = CompactMode ?? existing.CompactMode,
                ShowTooltips = ShowTooltips ?? existing.ShowTooltips,
                AnimationsEnabled = AnimationsEnabled ?? existing.AnimationsEnabled,
                SoundEnabled = SoundEnabled ?? existing.SoundEnabled,
                AutoSave = AutoSave ?? existing.AutoSave,
                WorkArea = WorkArea ?? existing.WorkArea,
                DashboardLayout = DashboardLayout ?? existing.DashboardLayout,
                QuickAccessItems = QuickAccessItems ?? existing.QuickAccessItems
            };
        }
    }
}
