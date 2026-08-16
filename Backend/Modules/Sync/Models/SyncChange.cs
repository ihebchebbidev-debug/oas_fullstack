using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Sync.Models
{
    [Table("sync_changes")]
    public class SyncChange : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [MaxLength(80)]
        public string EntityType { get; set; } = string.Empty;

        public int EntityId { get; set; }
        
        [MaxLength(128)]
        public string? EntityKey { get; set; }

        [Required]
        [MaxLength(20)]
        public string Operation { get; set; } = "update";

        [Column(TypeName = "text")]
        public string? DataJson { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? ChangedBy { get; set; }
        
        public int? ChangedByUserId { get; set; }
    }
}
