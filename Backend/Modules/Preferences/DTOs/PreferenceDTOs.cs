using System.Text.Json.Serialization;

namespace MyApi.Modules.Preferences.DTOs
{
    public class PreferencesData
    {
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "system";
        
        [JsonPropertyName("language")]
        public string Language { get; set; } = "en";
        
        [JsonPropertyName("primaryColor")]
        public string PrimaryColor { get; set; } = "blue";
        
        [JsonPropertyName("layoutMode")]
        public string LayoutMode { get; set; } = "sidebar";
        
        [JsonPropertyName("dataView")]
        public string DataView { get; set; } = "table";
        
        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; } = "UTC";
        
        [JsonPropertyName("dateFormat")]
        public string? DateFormat { get; set; } = "MM/DD/YYYY";
        
        [JsonPropertyName("timeFormat")]
        public string? TimeFormat { get; set; } = "12h";
        
        [JsonPropertyName("currency")]
        public string? Currency { get; set; } = "TND";
        
        [JsonPropertyName("numberFormat")]
        public string? NumberFormat { get; set; }
        
        [JsonPropertyName("notifications")]
        public string? Notifications { get; set; }
        
        [JsonPropertyName("sidebarCollapsed")]
        public bool SidebarCollapsed { get; set; } = false;
        
        [JsonPropertyName("compactMode")]
        public bool CompactMode { get; set; } = false;
        
        [JsonPropertyName("showTooltips")]
        public bool ShowTooltips { get; set; } = true;
        
        [JsonPropertyName("animationsEnabled")]
        public bool AnimationsEnabled { get; set; } = true;
        
        [JsonPropertyName("soundEnabled")]
        public bool SoundEnabled { get; set; } = false;
        
        [JsonPropertyName("autoSave")]
        public bool AutoSave { get; set; } = true;
        
        [JsonPropertyName("workArea")]
        public string? WorkArea { get; set; }
        
        [JsonPropertyName("dashboardLayout")]
        public string? DashboardLayout { get; set; }
        
        [JsonPropertyName("quickAccessItems")]
        public string? QuickAccessItems { get; set; }
    }

    public class UserPreferenceDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Theme { get; set; } = "system";
        public string Language { get; set; } = "en";
        public string PrimaryColor { get; set; } = "blue";
        public string LayoutMode { get; set; } = "sidebar";
        public string DataView { get; set; } = "table";
        public string? Timezone { get; set; }
        public string? DateFormat { get; set; }
        public string? TimeFormat { get; set; }
        public string? Currency { get; set; }
        public string? NumberFormat { get; set; }
        public string? Notifications { get; set; }
        public bool SidebarCollapsed { get; set; }
        public bool CompactMode { get; set; }
        public bool ShowTooltips { get; set; }
        public bool AnimationsEnabled { get; set; }
        public bool SoundEnabled { get; set; }
        public bool AutoSave { get; set; }
        public string? WorkArea { get; set; }
        public string? DashboardLayout { get; set; }
        public string? QuickAccessItems { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreatePreferenceDto
    {
        public string Theme { get; set; } = "system";
        public string Language { get; set; } = "en";
        public string PrimaryColor { get; set; } = "blue";
        public string LayoutMode { get; set; } = "sidebar";
        public string DataView { get; set; } = "table";
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
    }

    public class UpdatePreferenceDto
    {
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
    }
}