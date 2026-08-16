namespace MyApi.Modules.Preferences.DTOs
{
    public class PdfSettingsDto
    {
        public int Id { get; set; }
        public string Module { get; set; } = string.Empty;
        public object? SettingsJson { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreatePdfSettingsRequest
    {
        public string Module { get; set; } = string.Empty;
        public object? SettingsJson { get; set; }
    }

    public class UpdatePdfSettingsRequest
    {
        public object? SettingsJson { get; set; }
    }

    public class PdfSettingsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PdfSettingsDto? Data { get; set; }
    }

    public class PdfSettingsListResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<PdfSettingsDto>? Data { get; set; }
    }
}
