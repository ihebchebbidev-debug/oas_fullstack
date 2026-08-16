namespace MyApi.Modules.OAS.Integrations.DTOs;

public class OasIntegrationEndpointDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool HasSecret { get; set; }
    public List<string> EventTypes { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class OasIntegrationEndpointCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Secret { get; set; }
    public List<string> EventTypes { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class OasIntegrationEndpointUpdateRequest
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Secret { get; set; }
    public List<string>? EventTypes { get; set; }
    public bool? IsActive { get; set; }
}

public class OasIntegrationOutboxDto
{
    public Guid Id { get; set; }
    public Guid EndpointId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}

public class OasWebhookInRequest
{
    public string EventType { get; set; } = string.Empty;
    public object? Payload { get; set; }
}
