using Microsoft.EntityFrameworkCore;
using MyApi.Infrastructure;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Integrations.Models;

namespace MyApi.Modules.OAS.Integrations.HostedServices;

/// <summary>
/// Drains <c>oas_integration_outbox</c> (spec §6.1 Integrations: "file d'émission avec retry
/// et statut") — delivers each pending row to its endpoint's URL via HTTP POST, same
/// per-tenant-database sweep pattern as <c>OasEscalationSweepHostedService</c>. A row is
/// retried up to <see cref="MaxAttempts"/> times before being left in <c>failed</c> permanently
/// (no dead-letter queue — the outbox row itself, with its accumulated <c>last_error</c>, is
/// the record of the failure).
/// </summary>
public class OasIntegrationDeliveryHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private const int MaxAttempts = 5;
    private const int BatchSize = 50;

    private readonly IOasDbContextFactory _dbFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OasIntegrationDeliveryHostedService> _logger;

    public OasIntegrationDeliveryHostedService(IOasDbContextFactory dbFactory, IHttpClientFactory httpClientFactory, ILogger<OasIntegrationDeliveryHostedService> logger)
    {
        _dbFactory = dbFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var conn in TenantConnectionResolver.GetConfiguredTenantConnections())
            {
                if (!OasTenant.IsOasSlug(conn.Tenant)) continue;
                await DeliverOneAsync(conn.Tenant, stoppingToken);
            }
        }
    }

    private async Task DeliverOneAsync(string oasSlug, CancellationToken ct)
    {
        try
        {
            await using var db = _dbFactory.CreateDbContext(oasSlug);

            var pending = await db.Set<OasIntegrationOutbox>()
                .Where(o => o.Status == "pending" && o.Attempts < MaxAttempts)
                .OrderBy(o => o.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (pending.Count == 0) return;

            var endpointIds = pending.Select(o => o.EndpointId).Distinct().ToList();
            var endpoints = await db.Set<OasIntegrationEndpoint>()
                .Where(e => endpointIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, ct);

            var client = _httpClientFactory.CreateClient("oas-integration-delivery");
            client.Timeout = TimeSpan.FromSeconds(10);
            var delivered = 0;

            foreach (var row in pending)
            {
                if (!endpoints.TryGetValue(row.EndpointId, out var endpoint) || !endpoint.IsActive)
                {
                    row.Status = "failed";
                    row.LastError = "endpoint_inactive_or_deleted";
                    continue;
                }

                try
                {
                    using var content = new StringContent(row.Payload, System.Text.Encoding.UTF8, "application/json");
                    if (!string.IsNullOrEmpty(endpoint.Secret)) content.Headers.Add("X-Oas-Signature", endpoint.Secret);
                    var response = await client.PostAsync(endpoint.Url, content, ct);
                    row.Attempts++;
                    if (response.IsSuccessStatusCode)
                    {
                        row.Status = "sent";
                        row.SentAt = DateTimeOffset.UtcNow;
                        delivered++;
                    }
                    else
                    {
                        row.LastError = $"http_{(int)response.StatusCode}";
                        if (row.Attempts >= MaxAttempts) row.Status = "failed";
                    }
                }
                catch (Exception ex)
                {
                    row.Attempts++;
                    row.LastError = ex.Message;
                    if (row.Attempts >= MaxAttempts) row.Status = "failed";
                }
            }

            await db.SaveChangesAsync(ct);
            if (delivered > 0) _logger.LogInformation("🏭 OAS-INTEGRATION-DELIVERY: delivered {Count} outbox row(s) on '{Slug}'", delivered, oasSlug);
        }
        catch (OasTenantNotProvisionedException)
        {
            // not yet schema-provisioned — skip
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🏭 OAS-INTEGRATION-DELIVERY: failed for tenant '{Slug}'", oasSlug);
        }
    }
}
