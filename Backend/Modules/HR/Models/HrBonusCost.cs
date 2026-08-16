using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_bonus_costs")]
    public class HrBonusCost : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        // bonus | allowance | reimbursement | other_cost
        [Column("kind")]
        [MaxLength(40)]
        public string Kind { get; set; } = "bonus";

        // transport, panier, performance, custom...
        [Column("category")]
        [MaxLength(80)]
        public string? Category { get; set; }

        [Column("label")]
        [MaxLength(200)]
        public string Label { get; set; } = string.Empty;

        [Column("amount", TypeName = "decimal(14,3)")]
        public decimal Amount { get; set; }

        // monthly | one_off
        [Column("frequency")]
        [MaxLength(20)]
        public string Frequency { get; set; } = "monthly";

        // Year/Month it should be applied to (one_off only requires both)
        [Column("year")]
        public int Year { get; set; }

        [Column("month")]
        public int Month { get; set; }

        // If true, this item affects payroll (gross+) else informational cost
        [Column("affects_payroll")]
        public bool AffectsPayroll { get; set; } = true;

        // Optional: subject to CNSS or not (e.g. transport allowance often not)
        [Column("subject_to_cnss")]
        public bool SubjectToCnss { get; set; } = false;

        [Column("notes")]
        public string? Notes { get; set; }

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
