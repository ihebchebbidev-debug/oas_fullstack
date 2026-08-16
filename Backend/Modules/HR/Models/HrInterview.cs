using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_interviews")]
    public class HrInterview : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("applicant_id")]
        public int ApplicantId { get; set; }

        // phone | technical | hr | onsite | final
        [Column("kind")]
        [MaxLength(30)]
        public string Kind { get; set; } = "phone";

        [Column("scheduled_at")]
        public DateTime ScheduledAt { get; set; }

        [Column("duration_minutes")]
        public int DurationMinutes { get; set; } = 45;

        [Column("interviewer_user_id")]
        public int? InterviewerUserId { get; set; }

        [Column("location")]
        [MaxLength(200)]
        public string? Location { get; set; }

        [Column("meeting_url")]
        [MaxLength(500)]
        public string? MeetingUrl { get; set; }

        // scheduled | done | cancelled | no_show
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "scheduled";

        [Column("score")]
        public int? Score { get; set; } // 1..5

        [Column("feedback", TypeName = "text")]
        public string? Feedback { get; set; }

        // hire | no_hire | maybe | next_round | null
        [Column("recommendation")]
        [MaxLength(20)]
        public string? Recommendation { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;
    }
}