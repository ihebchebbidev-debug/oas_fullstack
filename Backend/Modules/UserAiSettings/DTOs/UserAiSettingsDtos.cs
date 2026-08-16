namespace MyApi.Modules.UserAiSettings.DTOs
{
    // ─── Key DTOs ───

    public class UserAiKeyDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Label { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;  // masked on GET
        public string Provider { get; set; } = "openrouter";
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateUserAiKeyDto
    {
        public string Label { get; set; } = "Key";
        public string ApiKey { get; set; } = string.Empty;
        public string Provider { get; set; } = "openrouter";
        public int Priority { get; set; } = 0;
    }

    public class UpdateUserAiKeyDto
    {
        public string? Label { get; set; }
        public string? ApiKey { get; set; }
        public int? Priority { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ReorderKeysDto
    {
        public List<int> KeyIds { get; set; } = new();
    }

    // ─── Preferences DTOs ───

    public class UserAiPreferencesDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? DefaultModel { get; set; }
        public string? FallbackModel { get; set; }
        public decimal DefaultTemperature { get; set; }
        public int DefaultMaxTokens { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateUserAiPreferencesDto
    {
        public string? DefaultModel { get; set; }
        public string? FallbackModel { get; set; }
        public decimal? DefaultTemperature { get; set; }
        public int? DefaultMaxTokens { get; set; }
    }
}
