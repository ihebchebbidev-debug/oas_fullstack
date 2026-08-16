using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    /// <summary>
    /// Tenant-configurable CNSS / payroll rates (Tunisia).
    /// Only one active row per tenant is expected; older rows kept for history.
    /// </summary>
    [ModuleScope("hr")]
    [Table("hr_cnss_rates")]
    public class HrCnssRate : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("effective_from")]
        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

        // Employee share (e.g. 0.0918 = 9.18%)
        [Column("employee_rate", TypeName = "decimal(8,6)")]
        public decimal EmployeeRate { get; set; } = 0.0918m;

        // Employer share (e.g. 0.1657 = 16.57%)
        [Column("employer_rate", TypeName = "decimal(8,6)")]
        public decimal EmployerRate { get; set; } = 0.1657m;

        // CSS - Contribution Sociale de Solidarité
        [Column("css_rate", TypeName = "decimal(8,6)")]
        public decimal CssRate { get; set; } = 0.01m;

        // Optional ceiling (TND), 0 = no ceiling
        [Column("salary_ceiling", TypeName = "decimal(14,3)")]
        public decimal SalaryCeiling { get; set; } = 0m;

        // Abattements (used by IRPP calculation)
        [Column("abattement_head_of_family", TypeName = "decimal(14,3)")]
        public decimal AbattementHeadOfFamily { get; set; } = 150m;

        [Column("abattement_per_child", TypeName = "decimal(14,3)")]
        public decimal AbattementPerChild { get; set; } = 100m;

        // IRPP brackets stored as JSON array: [{from,to,rate}]
        [Column("irpp_brackets_json", TypeName = "text")]
        public string? IrppBracketsJson { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("notes")]
        [MaxLength(300)]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
