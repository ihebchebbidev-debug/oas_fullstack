using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MyApi.Modules.Sync.DTOs
{
    public class SyncPushRequestDto
    {
        public string DeviceId { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public List<SyncOperationDto> Operations { get; set; } = new();
    }

    public class SyncOperationDto
    {
        public string OpId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public string? ClientTempId { get; set; }
        public JsonElement? Payload { get; set; }
        public DateTime? ClientTimestamp { get; set; }
        public string? Method { get; set; }
        public string? Endpoint { get; set; }
        public string? TransactionGroupId { get; set; }
        public string? ConflictStrategy { get; set; }
    }

    public class SyncPushResponseDto
    {
        public List<SyncPushResultDto> Results { get; set; } = new();
    }

    public class SyncPushResultDto
    {
        public string OpId { get; set; } = string.Empty;
        public string Status { get; set; } = "applied";
        public int? ServerEntityId { get; set; }
        public string? ServerEntityKey { get; set; }
        public int? ServerVersion { get; set; }
        public string? Error { get; set; }
    }

    public class SyncPullResponseDto
    {
        public List<SyncChangeDto> Changes { get; set; } = new();
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }

    public class SyncChangeDto
    {
        public long Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string? EntityKey { get; set; }
        public string Operation { get; set; } = string.Empty;
        public string? DataJson { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class SyncHistoryQueryDto
    {
        public string? Status { get; set; }
        public string? Search { get; set; }
        public string? DeviceId { get; set; }
        public bool IncludePayloads { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class SyncHistoryItemDto
    {
        public string OpId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? ServerEntityId { get; set; }
        public string? ServerEntityKey { get; set; }
        public string? EntityType { get; set; }
        public string? Operation { get; set; }
        public string? Endpoint { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Error { get; set; }
        public bool CanRetry { get; set; }
        public string? Method { get; set; }
        public string? TransactionGroupId { get; set; }
        public string? ConflictStrategy { get; set; }
        public string? OperationJson { get; set; }
        public string? ResponseJson { get; set; }
    }

    public class SyncHistoryResponseDto
    {
        public List<SyncHistoryItemDto> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    public class SyncRetryRequestDto
    {
        public string DeviceId { get; set; } = string.Empty;
        public string OpId { get; set; } = string.Empty;
    }
}
