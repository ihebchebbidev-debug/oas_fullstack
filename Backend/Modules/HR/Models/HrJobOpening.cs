using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_job_openings")]
    public class HrJobOpening : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Column("department_id")]
        public int? DepartmentId { get; set; }

        [Column("location")]
        [MaxLength(200)]
        public string? Location { get; set; }

        // CDI | CDD | Stage | Freelance
        [Column("contract_type")]
        [MaxLength(20)]
        public string? ContractType { get; set; }

        // junior | mid | senior | lead
        [Column("seniority")]
        [MaxLength(20)]
        public string? Seniority { get; set; }

        [Column("description", TypeName = "text")]
        public string? Description { get; set; }

        [Column("requirements", TypeName = "text")]
        public string? Requirements { get; set; }

        [Column("salary_min", TypeName = "decimal(14,3)")]
        public decimal? SalaryMin { get; set; }

        [Column("salary_max", TypeName = "decimal(14,3)")]
        public decimal? SalaryMax { get; set; }

        [Column("currency")]
        [MaxLength(8)]
        public string Currency { get; set; } = "TND";

        [Column("openings_count")]
        public int OpeningsCount { get; set; } = 1;

        // draft | open | on_hold | closed | filled
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "draft";

        [Column("hiring_manager_user_id")]
        public int? HiringManagerUserId { get; set; }

        [Column("opened_at")]
        public DateTime? OpenedAt { get; set; }

        [Column("closed_at")]
        public DateTime? ClosedAt { get; set; }

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