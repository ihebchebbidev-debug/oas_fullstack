using System.Text.Json.Serialization;

namespace MyApi.Modules.Processes.DTOs
{
    public class ProcessScheduleDto
    {
        [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("enabled")] public bool Enabled { get; set; }
        [JsonPropertyName("paused")] public bool Paused { get; set; }
        [JsonPropertyName("interval_minutes")] public int IntervalMinutes { get; set; }
        [JsonPropertyName("max_retries")] public int MaxRetries { get; set; }
        [JsonPropertyName("retry_backoff_seconds")] public int RetryBackoffSeconds { get; set; }
        [JsonPropertyName("config")] public object Config { get; set; } = new { };
        [JsonPropertyName("timezone")] public string Timezone { get; set; } = "UTC";
        [JsonPropertyName("next_run_at")] public DateTime? NextRunAt { get; set; }
        [JsonPropertyName("last_run_at")] public DateTime? LastRunAt { get; set; }
        [JsonPropertyName("last_status")] public string? LastStatus { get; set; }
        [JsonPropertyName("consecutive_failures")] public int ConsecutiveFailures { get; set; }
        [JsonPropertyName("block_reason")] public string? BlockReason { get; set; }
        [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }

        // ── live runtime state, projected from the most recent ProcessRun ──
        // The UI needs the *real* status and the *exact* failure text; without
        // these it could only guess from LastStatus and fell back to catalog
        // placeholder values.
        /// <summary>True when the latest run is still in flight (not stale).</summary>
        [JsonPropertyName("is_running")] public bool IsRunning { get; set; }
        /// <summary>Error message of the most recent run, when it failed.</summary>
        [JsonPropertyName("last_error")] public string? LastError { get; set; }
        [JsonPropertyName("last_duration_ms")] public int? LastDurationMs { get; set; }
        [JsonPropertyName("last_items_processed")] public int? LastItemsProcessed { get; set; }
        [JsonPropertyName("last_triggered_by")] public string? LastTriggeredBy { get; set; }
        [JsonPropertyName("last_attempt")] public int? LastAttempt { get; set; }
        [JsonPropertyName("next_retry_at")] public DateTime? NextRetryAt { get; set; }
        /// <summary>False when no handler is registered for this key — the process can never run.</summary>
        [JsonPropertyName("has_handler")] public bool HasHandler { get; set; } = true;
        /// <summary>Total runs recorded and how many succeeded (last 30 runs) — drives the success rate.</summary>
        [JsonPropertyName("recent_total")] public int RecentTotal { get; set; }
        [JsonPropertyName("recent_success")] public int RecentSuccess { get; set; }
    }

    public class UpsertScheduleRequest
    {
        [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("enabled")] public bool? Enabled { get; set; }
        [JsonPropertyName("paused")] public bool? Paused { get; set; }
        [JsonPropertyName("interval_minutes")] public int? IntervalMinutes { get; set; }
        [JsonPropertyName("max_retries")] public int? MaxRetries { get; set; }
        [JsonPropertyName("retry_backoff_seconds")] public int? RetryBackoffSeconds { get; set; }
        [JsonPropertyName("config")] public object? Config { get; set; }
        [JsonPropertyName("timezone")] public string? Timezone { get; set; }
    }

    public class ProcessRunDto
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("process_key")] public string ProcessKey { get; set; } = string.Empty;
        [JsonPropertyName("triggered_by")] public string TriggeredBy { get; set; } = "schedule";
        [JsonPropertyName("attempt")] public int Attempt { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "running";
        [JsonPropertyName("started_at")] public DateTime StartedAt { get; set; }
        [JsonPropertyName("finished_at")] public DateTime? FinishedAt { get; set; }
        [JsonPropertyName("duration_ms")] public int? DurationMs { get; set; }
        [JsonPropertyName("items_processed")] public int? ItemsProcessed { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("block_reason")] public string? BlockReason { get; set; }
        [JsonPropertyName("next_retry_at")] public DateTime? NextRetryAt { get; set; }
    }

    public class RunNowResult
    {
        [JsonPropertyName("status")] public string Status { get; set; } = "success";
        [JsonPropertyName("duration_ms")] public int DurationMs { get; set; }
        [JsonPropertyName("items_processed")] public int? ItemsProcessed { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("block_reason")] public string? BlockReason { get; set; }
        [JsonPropertyName("output")] public object? Output { get; set; }
    }
}
