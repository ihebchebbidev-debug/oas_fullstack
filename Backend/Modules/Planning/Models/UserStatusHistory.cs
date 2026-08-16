using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Planning.Models
{
    [ModuleScope("planning")]
    [Table("user_status_history")]
    public class UserStatusHistory : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("new_status")]
        [MaxLength(50)]
        public string NewStatus { get; set; } = null!;

        [Column("previous_status")]
        [MaxLength(50)]
        public string? PreviousStatus { get; set; }

        [Column("reason")]
        public string? Reason { get; set; }

        [Column("changed_at")]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [Column("changed_by")]
        public int? ChangedBy { get; set; }
    }
}
