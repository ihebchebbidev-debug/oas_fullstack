using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_public_holidays")]
    public class HrPublicHoliday : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("date")]
        public DateTime Date { get; set; }

        [Column("name")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        // Tunisia | religious | civil | custom
        [Column("category")]
        [MaxLength(50)]
        public string Category { get; set; } = "civil";

        [Column("is_recurring")]
        public bool IsRecurring { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
