using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Integrations.Models;

/// <summary>An outbound MES/ERP subscription: which event types get delivered to which URL.</summary>
public class OasIntegrationEndpoint : IOasTenantEntity
{
    public Guid Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Secret { get; set; }
    public List<string> EventTypes { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>An emission queue row: one event fanned out to one subscribed endpoint, delivered with retry.</summary>
public class OasIntegrationOutbox : IOasTenantEntity
{
    public Guid Id { get; set; }
    public int TenantId { get; set; }
    public Guid EndpointId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}
