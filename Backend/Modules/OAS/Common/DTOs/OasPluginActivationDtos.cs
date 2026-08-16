namespace MyApi.Modules.OAS.Common.DTOs;

public class OasPluginActivationDto
{
    public string PluginCode { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool IsCore { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class OasSetPluginActivationRequestDto
{
    public bool Enabled { get; set; }
}

public class OasBulkPluginActivationResponseDto
{
    public bool Success { get; set; }
    public IReadOnlyList<string> Reset { get; set; } = Array.Empty<string>();
}
