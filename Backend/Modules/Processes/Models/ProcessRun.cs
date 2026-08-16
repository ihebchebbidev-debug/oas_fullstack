using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApi.Modules.Processes.Models
{
    /// <summary>
    /// One execution attempt of a process. Populated by the scheduler for automatic runs,
    /// by the controller for manual "Run now" invocations, and by the retry loop.
    /// </summary>
    [Table("ProcessRuns")]
    public class ProcessRun
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required, MaxLength(120)]
        public string ProcessKey { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string TriggeredBy { get; set; } = "schedule"; // schedule | manual | retry

        public int Attempt { get; set; } = 1;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "running"; // running | success | failed | blocked | skipped

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }
        public int? DurationMs { get; set; }
        public int? ItemsProcessed { get; set; }

        public string? Error { get; set; }

        [MaxLength(500)]
        public string? BlockReason { get; set; }

        public DateTime? NextRetryAt { get; set; }

        /// <summary>Optional structured handler output for diagnostics.</summary>
        [Column(TypeName = "jsonb")]
        public string? OutputJson { get; set; }
    }
}
