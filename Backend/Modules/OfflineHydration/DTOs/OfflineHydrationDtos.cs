using System.Text.Json.Serialization;

namespace MyApi.Modules.OfflineHydration.DTOs;

public class OfflineHydrationModulesDto
{
    [JsonPropertyName("modules")]
    public Dictionary<string, bool> Modules { get; set; } = new();

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateOfflineHydrationModulesRequest
{
    [JsonPropertyName("modules")]
    public Dictionary<string, bool>? Modules { get; set; }
}
