using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Offline.DTOs;
using MyApi.Modules.OAS.Offline.Models;

namespace MyApi.Modules.OAS.Offline.Services;

public interface IOasOfflineSyncService
{
    Task<OasSyncPushResponseDto> PushAsync(int tenantId, OasSyncPushRequestDto request);
    Task<OasSyncPullResponseDto> PullAsync(int tenantId, DateTimeOffset since, int limit);
    Task<IReadOnlyList<OasSyncReceiptDto>> GetReceiptsAsync(int tenantId);

    Task<OasAttachmentDto> CreateAttachmentAsync(int tenantId, Guid? uploadedBy, OasAttachmentRequestDto request);
    Task<IReadOnlyList<OasAttachmentDto>> GetAttachmentsAsync(int tenantId, string entityTable, Guid entityId);
}

/// <summary>
/// Real POST /api/oas/sync/push (spec §6.3) — replaces `session.ts:504-543`'s
/// `setTimeout` + `Math.random() &lt; 0.2` simulated flaky sync. Idempotent per
/// ClientEventId: a genuine retry produces a "duplicate" receipt with the
/// SAME final outcome (200), never a second write — the actual entity
/// writes happen on each resource's own idempotent endpoint (declarations,
/// events, ...); this only tracks that the device attempted the push.
/// </summary>
public class OasOfflineSyncService : IOasOfflineSyncService
{
    private readonly OasDbContext _db;
    public OasOfflineSyncService(OasDbContext db) => _db = db;

    public async Task<OasSyncPushResponseDto> PushAsync(int tenantId, OasSyncPushRequestDto request)
    {
        var response = new OasSyncPushResponseDto();

        foreach (var item in request.Items)
        {
            var existing = await _db.Set<OasSyncReceipt>().FirstOrDefaultAsync(r => r.ClientEventId == item.ClientEventId);
            if (existing is not null)
            {
                existing.Attempts++;
                existing.Status = "duplicate";
                await _db.SaveChangesAsync();
                response.Receipts.Add(ToDto(existing));
                continue;
            }

            var receipt = new OasSyncReceipt
            {
                TenantId = tenantId, ClientEventId = item.ClientEventId, DeviceId = item.DeviceId,
                Entity = item.Entity, OccurredAt = item.OccurredAt, Status = "ok",
            };
            _db.Set<OasSyncReceipt>().Add(receipt);
            await _db.SaveChangesAsync();
            response.Receipts.Add(ToDto(receipt));
        }

        return response;
    }

    /// <summary>Lightweight combined feed of what changed since a timestamp — events and post-states are what a reconnecting device most urgently needs to catch up on.</summary>
    public async Task<OasSyncPullResponseDto> PullAsync(int tenantId, DateTimeOffset since, int limit)
    {
        var rows = await _db.Database.SqlQueryRaw<PullRow>(
            """
            (select 'event' as entity, id, updated_at from oas_events where updated_at > {0})
            union all
            (select 'post_state' as entity, post_id as id, updated_at from oas_post_states where updated_at > {0})
            order by updated_at desc
            limit {1}
            """, since, limit).ToListAsync();

        return new OasSyncPullResponseDto
        {
            Changes = rows.Select(r => new OasSyncPullEntryDto { Entity = r.entity, Id = r.id, UpdatedAt = r.updated_at }).ToList(),
        };
    }

    public async Task<IReadOnlyList<OasSyncReceiptDto>> GetReceiptsAsync(int tenantId)
    {
        var rows = await _db.Set<OasSyncReceipt>().OrderByDescending(r => r.ReceivedAt).Take(200).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<OasAttachmentDto> CreateAttachmentAsync(int tenantId, Guid? uploadedBy, OasAttachmentRequestDto request)
    {
        var attachment = new OasAttachment
        {
            TenantId = tenantId, Path = request.Path, MimeType = request.MimeType, SizeBytes = request.SizeBytes,
            EntityTable = request.EntityTable, EntityId = request.EntityId, UploadedBy = uploadedBy,
        };
        _db.Set<OasAttachment>().Add(attachment);
        await _db.SaveChangesAsync();
        return ToAttachmentDto(attachment);
    }

    public async Task<IReadOnlyList<OasAttachmentDto>> GetAttachmentsAsync(int tenantId, string entityTable, Guid entityId)
    {
        var rows = await _db.Set<OasAttachment>().Where(a => a.EntityTable == entityTable && a.EntityId == entityId).ToListAsync();
        return rows.Select(ToAttachmentDto).ToList();
    }

    private static OasAttachmentDto ToAttachmentDto(OasAttachment a) => new()
    {
        Id = a.Id, Path = a.Path, MimeType = a.MimeType, SizeBytes = a.SizeBytes,
        EntityTable = a.EntityTable, EntityId = a.EntityId, CreatedAt = a.CreatedAt,
    };

    private static OasSyncReceiptDto ToDto(OasSyncReceipt r) => new()
    {
        ClientEventId = r.ClientEventId, Entity = r.Entity, Status = r.Status, ReceivedAt = r.ReceivedAt, Attempts = r.Attempts,
    };

    private sealed class PullRow
    {
        public string entity { get; set; } = string.Empty;
        public Guid id { get; set; }
        public DateTimeOffset updated_at { get; set; }
    }
}
