using System.Text.Json.Serialization;

namespace MyApi.Modules.Reporting.DTOs;

public class ReportingFavoriteDto
{
    [JsonPropertyName("widgetId")] public string WidgetId { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("position")] public int Position { get; set; }
}

public class ReportingFavoritesResponse
{
    [JsonPropertyName("scope")] public string Scope { get; set; } = "default";
    [JsonPropertyName("items")] public List<ReportingFavoriteDto> Items { get; set; } = new();
}

public class UpsertReportingFavoriteRequest
{
    [JsonPropertyName("scope")] public string? Scope { get; set; }
    [JsonPropertyName("widgetId")] public string WidgetId { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("position")] public int Position { get; set; }
}

public class ReorderReportingFavoritesRequest
{
    [JsonPropertyName("scope")] public string? Scope { get; set; }
    [JsonPropertyName("orderedWidgetIds")] public List<string> OrderedWidgetIds { get; set; } = new();
}