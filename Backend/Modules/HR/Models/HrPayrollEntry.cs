using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_payroll_entries")]
    public class HrPayrollEntry : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("payroll_run_id")]
        public int PayrollRunId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("gross_salary")]
        public decimal GrossSalary { get; set; }

        [Column("cnss")]
        public decimal Cnss { get; set; }

        [Column("taxable_gross")]
        public decimal TaxableGross { get; set; }

        [Column("abattement")]
        public decimal Abattement { get; set; }

        [Column("taxable_base")]
        public decimal TaxableBase { get; set; }

        [Column("irpp")]
        public decimal Irpp { get; set; }

        [Column("css")]
        public decimal Css { get; set; }

        [Column("net_salary")]
        public decimal NetSalary { get; set; }

        [Column("worked_days")]
        public decimal WorkedDays { get; set; }

        [Column("total_hours")]
        public decimal TotalHours { get; set; }

        [Column("overtime_hours")]
        public decimal OvertimeHours { get; set; }

        [Column("leave_days")]
        public decimal LeaveDays { get; set; }

        [Column("details")]
        public string Details { get; set; } = "{}";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
