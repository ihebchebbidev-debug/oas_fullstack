using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_performance_reviews")]
    public class HrPerformanceReview : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("cycle_id")]
        public int CycleId { get; set; }

        [Column("reviewer_user_id")]
        public int? ReviewerUserId { get; set; }

        // pending | self_assessment | manager_review | completed | acknowledged
        [Column("status")]
        [MaxLength(30)]
        public string Status { get; set; } = "pending";

        // Self-assessment payload (free-form text answers / JSON)
        [Column("self_assessment", TypeName = "text")]
        public string? SelfAssessment { get; set; }

        [Column("self_assessment_submitted_at")]
        public DateTime? SelfAssessmentSubmittedAt { get; set; }

        [Column("manager_comments", TypeName = "text")]
        public string? ManagerComments { get; set; }

        // 0..5
        [Column("overall_score", TypeName = "decimal(4,2)")]
        public decimal? OverallScore { get; set; }

        // exceeds | meets | partially_meets | below | new_to_role
        [Column("rating")]
        [MaxLength(40)]
        public string? Rating { get; set; }

        [Column("strengths", TypeName = "text")]
        public string? Strengths { get; set; }

        [Column("improvements", TypeName = "text")]
        public string? Improvements { get; set; }

        [Column("development_plan", TypeName = "text")]
        public string? DevelopmentPlan { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [Column("acknowledged_at")]
        public DateTime? AcknowledgedAt { get; set; }

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