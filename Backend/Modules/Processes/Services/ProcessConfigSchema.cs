using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyApi.Modules.Processes.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    // Central, single-source-of-truth configuration schema for every process
    // handler. Handlers ask this class for values instead of hand-rolling
    // JsonDocument parsing + magic Fallback / Min / Max literals, and the same
    // schema is exposed via GET /api/processes/schemas so the frontend renders
    // labels, units, defaults and clamps that always match what the handler
    // actually applies at runtime. When a handler's default or bound changes,
    // change it here and nowhere else.
    //
    // Every field's LabelI18nKey and HelpI18nKey resolves under the
    // system module's processes.<lang>.json locale bundle so both languages
    // stay in sync automatically.
    // ─────────────────────────────────────────────────────────────────────────

    public enum ProcessConfigType { Int, Bool }

    public class ProcessConfigField
    {
        [JsonPropertyName("key")]           public string Key { get; init; } = "";
        [JsonPropertyName("type")]          public string Type { get; init; } = "int";
        [JsonPropertyName("label_i18n_key")] public string LabelI18nKey { get; init; } = "";
        [JsonPropertyName("help_i18n_key")]  public string? HelpI18nKey { get; init; }
        [JsonPropertyName("unit")]          public string? Unit { get; init; }
        [JsonPropertyName("fallback")]      public object Fallback { get; init; } = 0;
        [JsonPropertyName("min")]           public int? Min { get; init; }
        [JsonPropertyName("max")]           public int? Max { get; init; }
        [JsonPropertyName("step")]          public int? Step { get; init; }
    }

    public class ProcessConfigLimits
    {
        [JsonPropertyName("interval_minutes_min")] public int IntervalMinutesMin { get; init; } = 1;
        [JsonPropertyName("interval_minutes_max")] public int IntervalMinutesMax { get; init; } = 43_200;
        [JsonPropertyName("max_retries_min")]      public int MaxRetriesMin { get; init; } = 0;
        [JsonPropertyName("max_retries_max")]      public int MaxRetriesMax { get; init; } = 20;
        [JsonPropertyName("backoff_seconds_min")]  public int BackoffSecondsMin { get; init; } = 1;
        [JsonPropertyName("backoff_seconds_max")]  public int BackoffSecondsMax { get; init; } = 86_400;
    }

    public class ProcessSchemaEntry
    {
        [JsonPropertyName("key")]    public string Key { get; init; } = "";
        [JsonPropertyName("fields")] public ProcessConfigField[] Fields { get; init; } = System.Array.Empty<ProcessConfigField>();
        [JsonPropertyName("limits")] public ProcessConfigLimits Limits { get; init; } = new();
    }

    public static class ProcessConfigSchemas
    {
        // Reusable field factories. Every literal in a handler goes through here
        // so a change to a default or clamp lands in one place.

        private const string LabelPrefix = "config.fields";

        private static ProcessConfigField Days(string key, int fallback, int min, int max = 3650, string? helpKey = null)
            => new()
            {
                Key = key, Type = "int",
                LabelI18nKey = $"{LabelPrefix}.{key}.label",
                HelpI18nKey  = helpKey ?? $"{LabelPrefix}.{key}.help",
                Unit = "days",
                Fallback = fallback, Min = min, Max = max, Step = 1,
            };

        private static ProcessConfigField Hours(string key, int fallback, int min, int max)
            => new()
            {
                Key = key, Type = "int",
                LabelI18nKey = $"{LabelPrefix}.{key}.label",
                HelpI18nKey  = $"{LabelPrefix}.{key}.help",
                Unit = "hours",
                Fallback = fallback, Min = min, Max = max, Step = 1,
            };

        private static ProcessConfigField Count(string key, int fallback, int min, int max)
            => new()
            {
                Key = key, Type = "int",
                LabelI18nKey = $"{LabelPrefix}.{key}.label",
                HelpI18nKey  = $"{LabelPrefix}.{key}.help",
                Unit = "count",
                Fallback = fallback, Min = min, Max = max, Step = 1,
            };

        // Tighter interval floor for heavy purge jobs — a "run every 1 minute"
        // purge is almost never what an admin means and can lock large tables.
        private static readonly ProcessConfigLimits PurgeLimits = new()
        {
            IntervalMinutesMin = 5,
            IntervalMinutesMax = 43_200,
        };

        public static readonly IReadOnlyDictionary<string, ProcessSchemaEntry> All =
            new Dictionary<string, ProcessSchemaEntry>(System.StringComparer.OrdinalIgnoreCase)
        {
            // 1
            ["admin.invoices-mark-overdue"] = new()
            {
                Key = "admin.invoices-mark-overdue",
                Fields = new[] { Days("grace_days", 0, 0, 60) },
            },
            // 2
            ["admin.offers-mark-expired"] = new()
            {
                Key = "admin.offers-mark-expired",
                Fields = new[] { Days("grace_days", 0, 0, 60) },
            },
            // 3
            ["admin.dispatches-mark-missed"] = new()
            {
                Key = "admin.dispatches-mark-missed",
                Fields = new[] { Hours("grace_hours", 2, 1, 168) },
            },
            // 4
            ["admin.payment-installments-mark-overdue"] = new()
            {
                Key = "admin.payment-installments-mark-overdue",
                Fields = new[] { Days("grace_days", 0, 0, 60) },
            },
            // 5
            ["admin.support-tickets-autoclose-resolved"] = new()
            {
                Key = "admin.support-tickets-autoclose-resolved",
                Fields = new[] { Days("days_resolved", 7, 1, 365) },
            },
            // 6
            ["admin.draft-offers-purge"] = new()
            {
                Key = "admin.draft-offers-purge",
                Fields = new[] { Days("age_days", 60, 7, 3650) },
                Limits = PurgeLimits,
            },
            // 7
            ["admin.draft-invoices-purge"] = new()
            {
                Key = "admin.draft-invoices-purge",
                Fields = new[] { Days("age_days", 60, 7, 3650) },
                Limits = PurgeLimits,
            },
            // 8
            ["admin.notifications-purge-read"] = new()
            {
                Key = "admin.notifications-purge-read",
                Fields = new[] { Days("age_days", 30, 1, 3650) },
                Limits = PurgeLimits,
            },
            // 9
            ["admin.notifications-purge-stale-unread"] = new()
            {
                Key = "admin.notifications-purge-stale-unread",
                Fields = new[] { Days("age_days", 180, 30, 3650) },
                Limits = PurgeLimits,
            },
            // 10
            ["admin.calendar-events-purge-past"] = new()
            {
                Key = "admin.calendar-events-purge-past",
                Fields = new[] { Days("age_days", 180, 30, 3650) },
                Limits = PurgeLimits,
            },
            // 11
            ["admin.sync-changes-purge"] = new()
            {
                Key = "admin.sync-changes-purge",
                Fields = new[] { Days("age_days", 30, 1, 3650) },
                Limits = PurgeLimits,
            },
            // 12
            ["admin.sync-receipts-purge"] = new()
            {
                Key = "admin.sync-receipts-purge",
                Fields = new[] { Days("age_days", 30, 1, 3650) },
                Limits = PurgeLimits,
            },
            // 13
            ["admin.webhook-jobs-purge"] = new()
            {
                Key = "admin.webhook-jobs-purge",
                Fields = new[] { Days("age_days", 30, 1, 3650) },
                Limits = PurgeLimits,
            },
            // 14 — retention is primarily per-endpoint; fallback covers rows with no value.
            ["admin.external-endpoint-logs-purge"] = new()
            {
                Key = "admin.external-endpoint-logs-purge",
                Fields = new[] { Days("fallback_retention_days", 30, 1, 3650) },
                Limits = PurgeLimits,
            },
            // 15
            ["admin.dispatch-audit-purge"] = new()
            {
                Key = "admin.dispatch-audit-purge",
                Fields = new[] { Days("age_days", 180, 30, 3650) },
                Limits = PurgeLimits,
            },
            // 16
            ["admin.hr-audit-purge"] = new()
            {
                Key = "admin.hr-audit-purge",
                Fields = new[] { Days("age_days", 365, 90, 3650) },
                Limits = PurgeLimits,
            },
            // 17
            ["admin.soft-deleted-purge"] = new()
            {
                Key = "admin.soft-deleted-purge",
                Fields = new[] { Days("age_days", 90, 30, 3650) },
                Limits = PurgeLimits,
            },
            // 18
            ["admin.recurring-task-logs-purge"] = new()
            {
                Key = "admin.recurring-task-logs-purge",
                Fields = new[] { Days("age_days", 180, 30, 3650) },
                Limits = PurgeLimits,
            },
            // 19
            ["admin.purge-system-logs"] = new()
            {
                Key = "admin.purge-system-logs",
                Fields = new[]
                {
                    Days("retention_days", 30, 1, 3650),
                    // Handler additionally floors run history at 30 days regardless
                    // of retention_days — keeps the process audit trail even when
                    // system-log retention is aggressively shortened.
                    Days("run_retention_days", 30, 30, 3650),
                },
                Limits = PurgeLimits,
            },
            // 20
            ["admin.retry-failed-emails"] = new()
            {
                Key = "admin.retry-failed-emails",
                Fields = new[] { Count("batch_size", 50, 1, 500) },
            },
        };

        public static ProcessSchemaEntry? For(string key) =>
            All.TryGetValue(key, out var e) ? e : null;

        // ── Runtime accessors used by handlers ─────────────────────────────

        /// <summary>
        /// Read an int config value using the schema entry's fallback + clamp.
        /// Accepts numbers and numeric strings (matches the pre-schema behaviour
        /// so a schedule row saved with "42" still works).
        /// </summary>
        public static int GetInt(string processKey, string configJson, string fieldKey)
        {
            var field = FindField(processKey, fieldKey)
                ?? throw new System.InvalidOperationException(
                    $"No schema field '{fieldKey}' declared for process '{processKey}'");
            var fallback = System.Convert.ToInt32(field.Fallback);
            var min = field.Min ?? int.MinValue;
            var max = field.Max ?? int.MaxValue;

            if (string.IsNullOrWhiteSpace(configJson)) return fallback;
            try
            {
                using var doc = JsonDocument.Parse(configJson);
                if (!doc.RootElement.TryGetProperty(fieldKey, out var v)) return fallback;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i))
                    return System.Math.Clamp(i, min, max);
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var si))
                    return System.Math.Clamp(si, min, max);
            }
            catch { /* malformed JSON → fall back */ }
            return fallback;
        }

        private static ProcessConfigField? FindField(string processKey, string fieldKey)
        {
            if (!All.TryGetValue(processKey, out var entry)) return null;
            for (int i = 0; i < entry.Fields.Length; i++)
                if (string.Equals(entry.Fields[i].Key, fieldKey, System.StringComparison.OrdinalIgnoreCase))
                    return entry.Fields[i];
            return null;
        }

        // ── Upsert validation ──────────────────────────────────────────────

        /// <summary>
        /// Normalise an incoming config object against the schema:
        /// coerces types and clamps declared numbers to [min, max]. Keys the
        /// schema does not declare are preserved verbatim — some handlers read
        /// free-form values not declared by the schema
        /// and silently dropping them would erase admin configuration.
        /// Returns the sanitised JSON string ready for storage.
        /// </summary>
        public static string SanitiseConfig(string processKey, object? incoming)
        {
            if (incoming == null) return "{}";

            // Round-trip through JsonDocument so we handle both Dictionary<...>
            // and JsonElement inputs uniformly.
            var raw = JsonSerializer.Serialize(incoming);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return "{}";

            // No schema declared for this key: store the object unchanged.
            if (!All.TryGetValue(processKey, out var entry)) return raw;

            var clean = new Dictionary<string, object?>();
            var declared = new HashSet<string>(entry.Fields.Select(f => f.Key), System.StringComparer.OrdinalIgnoreCase);
            foreach (var prop in root.EnumerateObject())
                if (!declared.Contains(prop.Name))
                    clean[prop.Name] = prop.Value.Clone();

            foreach (var f in entry.Fields)
            {
                if (!root.TryGetProperty(f.Key, out var v)) continue;

                if (f.Type == "int")
                {
                    int? n = null;
                    if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) n = i;
                    else if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var si)) n = si;
                    if (n.HasValue)
                        clean[f.Key] = System.Math.Clamp(n.Value, f.Min ?? int.MinValue, f.Max ?? int.MaxValue);
                }
                else if (f.Type == "bool")
                {
                    if (v.ValueKind == JsonValueKind.True)  clean[f.Key] = true;
                    if (v.ValueKind == JsonValueKind.False) clean[f.Key] = false;
                }
            }
            return JsonSerializer.Serialize(clean);
        }
    }
}
