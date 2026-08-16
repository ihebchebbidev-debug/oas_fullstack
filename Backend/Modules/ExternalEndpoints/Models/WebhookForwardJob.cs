using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.ExternalEndpoints.Models
{
    /// <summary>
    /// Durable queue row for outbound webhook forwards. Persisted so that pending
    /// jobs survive process restarts; the in-memory Channel is only a fast wake-up
    /// signal — the database is the source of truth.
    ///
    /// Lifecycle:
    ///   pending      → newly enqueued, waiting for NextAttemptAt
    ///   in_progress  → claimed by a worker (has ClaimedAt + ClaimedBy)
    ///   succeeded    → 2xx response received
    ///   failed       → transient failure, will be retried (Status flips back to pending)
    ///   dead_letter  → exhausted MaxAttempts; surfaced for manual inspection
    /// </summary>
    [Table("WebhookForwardJobs")]
    public class WebhookForwardJob : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("EndpointId")]
        public int EndpointId { get; set; }

        /// <summary>Linked log row (the inbound request that caused this forward).</summary>
        [Column("LogId")]
        public int? LogId { get; set; }

        [Required]
        [Column("ForwardUrl")]
        [MaxLength(2048)]
        public string ForwardUrl { get; set; } = string.Empty;

        [Column("Body")]
        public string? Body { get; set; }

        /// <summary>HMAC-SHA256 secret snapshot — captured at enqueue time so a
        /// later rotation of the endpoint's ForwardSecret doesn't invalidate
        /// in-flight retries.</summary>
        [Column("Secret")]
        [MaxLength(128)]
        public string? Secret { get; set; }

        [Required]
        [Column("Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        [Column("Attempts")]
        public int Attempts { get; set; } = 0;

        [Column("MaxAttempts")]
        public int MaxAttempts { get; set; } = 5;

        [Required]
        [Column("NextAttemptAt")]
        public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

        [Column("LastAttemptAt")]
        public DateTime? LastAttemptAt { get; set; }

        [Column("LastStatusCode")]
        public int? LastStatusCode { get; set; }

        [Column("LastError")]
        public string? LastError { get; set; }

        /// <summary>Set when a worker claims the row; cleared on completion.</summary>
        [Column("ClaimedAt")]
        public DateTime? ClaimedAt { get; set; }

        [Column("ClaimedBy")]
        [MaxLength(100)]
        public string? ClaimedBy { get; set; }

        [Required]
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("CompletedAt")]
        public DateTime? CompletedAt { get; set; }

        [ForeignKey("EndpointId")]
        public virtual ExternalEndpoint? Endpoint { get; set; }
    }
}
