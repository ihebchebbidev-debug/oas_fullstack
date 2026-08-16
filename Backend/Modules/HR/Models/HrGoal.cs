using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_goals")]
    public class HrGoal : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        // Optional link to a review cycle
        [Column("cycle_id")]
        public int? CycleId { get; set; }

        [Column("title")]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        // smart | okr | kpi | other
        [Column("category")]
        [MaxLength(40)]
        public string Category { get; set; } = "smart";

        [Column("weight", TypeName = "decimal(5,2)")]
        public decimal Weight { get; set; } = 0m; // % of overall score

        [Column("target_value")]
        [MaxLength(120)]
        public string? TargetValue { get; set; }

        // 0..100
        [Column("progress")]
        public int Progress { get; set; } = 0;

        // not_started | in_progress | achieved | partially | missed | cancelled
        [Column("status")]
        [MaxLength(30)]
        public string Status { get; set; } = "not_started";

        [Column("due_date")]
        public DateTime? DueDate { get; set; }

        // 0..5 stars - only filled at review time
        [Column("score", TypeName = "decimal(4,2)")]
        public decimal? Score { get; set; }

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