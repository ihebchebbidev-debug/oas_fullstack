using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_review_cycles")]
    public class HrReviewCycle : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        // annual | semi_annual | quarterly | custom
        [Column("frequency")]
        [MaxLength(20)]
        public string Frequency { get; set; } = "annual";

        [Column("period_start")]
        public DateTime PeriodStart { get; set; }

        [Column("period_end")]
        public DateTime PeriodEnd { get; set; }

        // draft | open | closed
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "draft";

        [Column("self_assessment_required")]
        public bool SelfAssessmentRequired { get; set; } = true;

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