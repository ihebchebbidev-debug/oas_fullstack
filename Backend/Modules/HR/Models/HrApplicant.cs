using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_applicants")]
    public class HrApplicant : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("opening_id")]
        public int OpeningId { get; set; }

        [Column("first_name")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Column("email")]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Column("phone")]
        [MaxLength(40)]
        public string? Phone { get; set; }

        [Column("source")]
        [MaxLength(60)]
        public string? Source { get; set; } // linkedin | referral | website | other

        [Column("resume_url")]
        [MaxLength(500)]
        public string? ResumeUrl { get; set; }

        [Column("resume_file_name")]
        [MaxLength(200)]
        public string? ResumeFileName { get; set; }

        // Pipeline stage:
        // applied | screening | interview | offer | hired | rejected | withdrawn
        [Column("stage")]
        [MaxLength(30)]
        public string Stage { get; set; } = "applied";

        [Column("rating")]
        public int? Rating { get; set; } // 1..5

        [Column("expected_salary", TypeName = "decimal(14,3)")]
        public decimal? ExpectedSalary { get; set; }

        [Column("available_from")]
        public DateTime? AvailableFrom { get; set; }

        [Column("rejection_reason")]
        [MaxLength(300)]
        public string? RejectionReason { get; set; }

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