using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Planning.Models
{
    [ModuleScope("planning")]
    [Table("technician_status_history")]
    public class TechnicianStatusHistory : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("technician_id")]
        public int TechnicianId { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = null!;

        [Column("changed_from")]
        [MaxLength(50)]
        public string? ChangedFrom { get; set; }

        [Column("changed_at")]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [Column("changed_by")]
        public int? ChangedBy { get; set; }

        [Column("reason")]
        public string? Reason { get; set; }

        [Column("metadata", TypeName = "jsonb")]
        public string? MetadataJson { get; set; }
    }
}
