namespace MyApi.Modules.Settings.DTOs
{
    public class AppSettingDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateAppSettingRequest
    {
        public string Value { get; set; } = string.Empty;
    }
}
