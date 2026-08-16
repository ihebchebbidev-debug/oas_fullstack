using System.Text.Json.Serialization;

namespace MyApi.Modules.Dashboards.DTOs;

public class DashboardLayoutDto
{
    [JsonPropertyName("scope")] public string Scope { get; set; } = "default";
    [JsonPropertyName("order")] public List<string> Order { get; set; } = new();
    [JsonPropertyName("hidden")] public List<string> Hidden { get; set; } = new();
}

public class SaveDashboardLayoutRequest
{
    [JsonPropertyName("scope")] public string? Scope { get; set; }
    [JsonPropertyName("order")] public List<string> Order { get; set; } = new();
    [JsonPropertyName("hidden")] public List<string> Hidden { get; set; } = new();
}
