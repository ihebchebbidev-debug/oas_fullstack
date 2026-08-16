using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_attendance_settings")]
    public class HrAttendanceSettings : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("work_days_json")]
        public string WorkDaysJson { get; set; } = "[1,2,3,4,5]";

        [Column("standard_hours_per_day", TypeName = "decimal(10,2)")]
        public decimal StandardHoursPerDay { get; set; } = 8m;

        [Column("overtime_threshold_hours", TypeName = "decimal(10,2)")]
        public decimal OvertimeThresholdHours { get; set; } = 8m;

        [Column("overtime_multiplier", TypeName = "decimal(10,2)")]
        public decimal OvertimeMultiplier { get; set; } = 1.75m;

        [Column("late_threshold_minutes")]
        public int LateThresholdMinutes { get; set; } = 15;

        [Column("rounding_method")]
        [MaxLength(30)]
        public string RoundingMethod { get; set; } = "15min";

        [Column("calculation_method")]
        [MaxLength(30)]
        public string CalculationMethod { get; set; } = "actual_hours";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}