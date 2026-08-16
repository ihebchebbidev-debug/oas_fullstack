using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    /// <summary>
    /// Versioned record of an employee's gross salary over time.
    /// Created automatically whenever HrEmployeeSalaryConfig.GrossSalary changes.
    /// </summary>
    [ModuleScope("hr")]
    [Table("hr_salary_history")]
    public class HrSalaryHistory : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("previous_gross", TypeName = "decimal(14,3)")]
        public decimal? PreviousGross { get; set; }

        [Column("new_gross", TypeName = "decimal(14,3)")]
        public decimal NewGross { get; set; }

        [Column("currency")]
        [MaxLength(10)]
        public string Currency { get; set; } = "TND";

        [Column("effective_date")]
        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

        [Column("reason")]
        [MaxLength(300)]
        public string? Reason { get; set; }

        [Column("changed_by")]
        public int? ChangedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
