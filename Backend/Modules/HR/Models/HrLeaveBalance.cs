using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_leave_balances")]
    public class HrLeaveBalance : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("year")]
        public int Year { get; set; }

        [Column("leave_type")]
        [MaxLength(50)]
        public string LeaveType { get; set; } = "annual";

        [Column("annual_allowance")]
        public decimal AnnualAllowance { get; set; }

        [Column("used")]
        public decimal Used { get; set; }

        [Column("pending")]
        public decimal Pending { get; set; }

        [Column("remaining")]
        public decimal Remaining { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
