using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApi.Modules.EmailAccounts.Models
{
    /// <summary>
    /// One outbound email attempt. Written by <c>EmailAccountService.SendEmailAsync</c>
    /// for every send — success or failure — so operators can trace every email that
    /// left (or tried to leave) the app together with the exact provider error.
    ///
    /// Failed rows with <see cref="Attempts"/> &lt; <see cref="MaxAttempts"/> are picked
    /// up by <c>admin.retry-failed-emails</c> and re-sent through the original account.
    /// Not tenant-scoped: the row stores <see cref="TenantId"/> as a plain column so
    /// admins can filter without the global tenant filter hiding cross-tenant history.
    /// </summary>
    [Table("OutboundEmailLogs")]
    public class OutboundEmailLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public Guid? AccountId { get; set; }
        public int? UserId { get; set; }
        public int TenantId { get; set; }

        [MaxLength(40)] public string Provider { get; set; } = "";
        [MaxLength(320)] public string? FromHandle { get; set; }

        /// <summary>Comma-separated recipient list (To only) for quick display.</summary>
        [MaxLength(2000)] public string? ToSummary { get; set; }

        [MaxLength(500)] public string? Subject { get; set; }

        /// <summary>Original SendEmailDto serialized so a retry can replay the exact payload.</summary>
        [Column(TypeName = "jsonb")]
        public string PayloadJson { get; set; } = "{}";

        /// <summary>pending | success | failed | gave_up</summary>
        [Required, MaxLength(20)]
        public string Status { get; set; } = "pending";

        public int Attempts { get; set; } = 0;
        public int MaxAttempts { get; set; } = 5;

        [MaxLength(200)] public string? MessageId { get; set; }
        public string? LastError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? NextRetryAt { get; set; }
    }
}
