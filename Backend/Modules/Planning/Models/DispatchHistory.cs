using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Planning.Models
{
    [ModuleScope("planning")]
    [Table("dispatch_history")]
    public class DispatchHistory : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("dispatch_id")]
        [MaxLength(50)]
        public string DispatchId { get; set; } = null!;

        [Required]
        [Column("action")]
        [MaxLength(50)]
        public string Action { get; set; } = null!;

        [Column("old_value")]
        public string? OldValue { get; set; }

        [Column("new_value")]
        public string? NewValue { get; set; }

        [Column("changed_by")]
        [MaxLength(50)]
        public string? ChangedBy { get; set; }

        [Column("changed_at")]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [Column("metadata", TypeName = "jsonb")]
        public string? MetadataJson { get; set; }
    }
}
