using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_payroll_runs")]
    public class HrPayrollRun : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("month")]
        public int Month { get; set; }

        [Column("year")]
        public int Year { get; set; }

        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "draft";

        [Column("total_gross")]
        public decimal TotalGross { get; set; }

        [Column("total_net")]
        public decimal TotalNet { get; set; }

        [Column("created_by")]
        public int CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("confirmed_at")]
        public DateTime? ConfirmedAt { get; set; }
    }
}
