using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApi.Modules.Processes.Models
{
    /// <summary>
    /// A registered administration process (scheduled background job) with its
    /// current runtime state. One row per process key (e.g. "admin.retry-failed-emails").
    /// Global (not tenant-scoped) — admin/system-level automation.
    /// </summary>
    [Table("ProcessSchedules")]
    public class ProcessSchedule
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Key { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;
        public bool Paused { get; set; } = false;

        /// <summary>Interval between automatic runs in minutes.</summary>
        public int IntervalMinutes { get; set; } = 60;

        /// <summary>Maximum retry attempts after a failed run before the schedule is marked blocked.</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>Base backoff in seconds. Effective delay = Backoff * 2^(attempt-1).</summary>
        public int RetryBackoffSeconds { get; set; } = 60;

        /// <summary>Free-form JSON configuration passed to the handler.</summary>
        [Column(TypeName = "jsonb")]
        public string ConfigJson { get; set; } = "{}";

        /// <summary>
        /// Informational only. Scheduling is purely interval-based (IntervalMinutes)
        /// and every computation uses DateTime.UtcNow, so there is no wall-clock slot
        /// for a timezone to shift. Kept as UTC; do not expose it as an editable field
        /// until cron/time-of-day scheduling actually exists.
        /// </summary>
        [MaxLength(60)]
        public string Timezone { get; set; } = "UTC";

        public DateTime? NextRunAt { get; set; }
        public DateTime? LastRunAt { get; set; }

        [MaxLength(20)]
        public string? LastStatus { get; set; } // success | failed | blocked | skipped

        public int ConsecutiveFailures { get; set; } = 0;

        [MaxLength(500)]
        public string? BlockReason { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
