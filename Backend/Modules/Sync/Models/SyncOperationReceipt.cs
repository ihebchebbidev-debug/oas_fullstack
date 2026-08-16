using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Sync.Models
{
    [Table("sync_operation_receipts")]
    public class SyncOperationReceipt : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string DeviceId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string OpId { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        public string Status { get; set; } = "applied";

        public int? ServerEntityId { get; set; }
        
        [MaxLength(128)]
        public string? ServerEntityKey { get; set; }

        [Column(TypeName = "text")]
        public string? ResponseJson { get; set; }

        [Column(TypeName = "text")]
        public string? OperationJson { get; set; }

        [MaxLength(256)]
        public string? CreatedByUser { get; set; }
        
        public int? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
