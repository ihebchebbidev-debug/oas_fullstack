using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_audit_logs")]
    public class HrAuditLog : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        // employee being affected
        [Column("user_id")]
        public int UserId { get; set; }

        // salary_change | bonus_added | bonus_removed | leave_requested | leave_approved | leave_rejected
        // payroll_generated | payroll_confirmed | cnss_rate_changed | document_uploaded | profile_updated
        [Column("event_type")]
        [MaxLength(60)]
        public string EventType { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        // JSON snapshot of before/after values
        [Column("payload", TypeName = "text")]
        public string? Payload { get; set; }

        // Actor
        [Column("actor_user_id")]
        public int? ActorUserId { get; set; }

        [Column("actor_name")]
        [MaxLength(200)]
        public string? ActorName { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
