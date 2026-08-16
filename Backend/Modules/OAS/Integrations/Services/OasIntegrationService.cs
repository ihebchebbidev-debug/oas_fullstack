using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Integrations.DTOs;
using MyApi.Modules.OAS.Integrations.Models;

namespace MyApi.Modules.OAS.Integrations.Services;

public interface IOasIntegrationService
{
    Task<IReadOnlyList<OasIntegrationEndpointDto>> GetEndpointsAsync(int tenantId);
    Task<OasIntegrationEndpointDto> CreateEndpointAsync(int tenantId, OasIntegrationEndpointCreateRequest request);
    Task<bool> UpdateEndpointAsync(int tenantId, Guid id, OasIntegrationEndpointUpdateRequest request);
    Task<bool> DeleteEndpointAsync(int tenantId, Guid id);
    Task<IReadOnlyList<OasIntegrationOutboxDto>> GetOutboxAsync(int tenantId, string? status);
    Task<(bool ok, string? error, int fannedOut)> ReceiveWebhookAsync(int tenantId, OasWebhookInRequest request);
}

/// <summary>
/// Outbound MES/ERP integration: <see cref="OasIntegrationEndpoint"/> rows are subscriptions
/// (which URL wants which event types); <see cref="ReceiveWebhookAsync"/> is the ingress every
/// other OAS sub-module (or an external trigger) calls to fan an event out into
/// <see cref="OasIntegrationOutbox"/> rows, one per matching active endpoint. Delivery + retry
/// is handled by <c>OasIntegrationDeliveryHostedService</c>, matching the SLA-sweep pattern.
/// </summary>
public class OasIntegrationService : IOasIntegrationService
{
    private readonly OasDbContext _db;
    public OasIntegrationService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasIntegrationEndpointDto>> GetEndpointsAsync(int tenantId)
    {
        var rows = await _db.Set<OasIntegrationEndpoint>().OrderBy(e => e.Name).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<OasIntegrationEndpointDto> CreateEndpointAsync(int tenantId, OasIntegrationEndpointCreateRequest request)
    {
        var endpoint = new OasIntegrationEndpoint
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = request.Name, Url = request.Url,
            Secret = request.Secret, EventTypes = request.EventTypes, IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Set<OasIntegrationEndpoint>().Add(endpoint);
        await _db.SaveChangesAsync();
        return ToDto(endpoint);
    }

    public async Task<bool> UpdateEndpointAsync(int tenantId, Guid id, OasIntegrationEndpointUpdateRequest request)
    {
        var endpoint = await _db.Set<OasIntegrationEndpoint>().FirstOrDefaultAsync(e => e.Id == id);
        if (endpoint is null) return false;
        if (request.Name is not null) endpoint.Name = request.Name;
        if (request.Url is not null) endpoint.Url = request.Url;
        if (request.Secret is not null) endpoint.Secret = request.Secret;
        if (request.EventTypes is not null) endpoint.EventTypes = request.EventTypes;
        if (request.IsActive is not null) endpoint.IsActive = request.IsActive.Value;
        endpoint.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteEndpointAsync(int tenantId, Guid id)
    {
        var endpoint = await _db.Set<OasIntegrationEndpoint>().FirstOrDefaultAsync(e => e.Id == id);
        if (endpoint is null) return false;
        _db.Set<OasIntegrationEndpoint>().Remove(endpoint);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<OasIntegrationOutboxDto>> GetOutboxAsync(int tenantId, string? status)
    {
        var query = _db.Set<OasIntegrationOutbox>().AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);
        var rows = await query.OrderByDescending(o => o.CreatedAt).Take(500).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<(bool ok, string? error, int fannedOut)> ReceiveWebhookAsync(int tenantId, OasWebhookInRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventType)) return (false, "event_type_required", 0);

        var endpoints = await _db.Set<OasIntegrationEndpoint>()
            .Where(e => e.IsActive && e.EventTypes.Contains(request.EventType))
            .ToListAsync();
        if (endpoints.Count == 0) return (true, null, 0);

        var payloadJson = System.Text.Json.JsonSerializer.Serialize(request.Payload ?? new { });
        var now = DateTimeOffset.UtcNow;
        foreach (var endpoint in endpoints)
        {
            _db.Set<OasIntegrationOutbox>().Add(new OasIntegrationOutbox
            {
                Id = Guid.NewGuid(), TenantId = tenantId, EndpointId = endpoint.Id,
                EventType = request.EventType, Payload = payloadJson, Status = "pending",
                Attempts = 0, CreatedAt = now,
            });
        }
        await _db.SaveChangesAsync();
        return (true, null, endpoints.Count);
    }

    private static OasIntegrationEndpointDto ToDto(OasIntegrationEndpoint e) => new()
    {
        Id = e.Id, Name = e.Name, Url = e.Url, HasSecret = !string.IsNullOrEmpty(e.Secret),
        EventTypes = e.EventTypes, IsActive = e.IsActive, CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt,
    };

    private static OasIntegrationOutboxDto ToDto(OasIntegrationOutbox o) => new()
    {
        Id = o.Id, EndpointId = o.EndpointId, EventType = o.EventType, Payload = o.Payload,
        Status = o.Status, Attempts = o.Attempts, LastError = o.LastError, CreatedAt = o.CreatedAt, SentAt = o.SentAt,
    };
}
