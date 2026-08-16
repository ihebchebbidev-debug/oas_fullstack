using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Dispatches.Models
{
    [ModuleScope("dispatches")]
    [Table("DispatchTechnicians")]
    public class DispatchTechnician : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DispatchId")]
        public int DispatchId { get; set; }

        [Required]
        [Column("TechnicianId")]
        public int TechnicianId { get; set; }

        [Column("AssignedDate")]
        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

        [Column("Role")]
        [MaxLength(50)]
        public string? Role { get; set; }

        [Column("IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("DeletedAt")]
        public DateTime? DeletedAt { get; set; }

        [Column("DeletedBy")]
        [MaxLength(100)]
        public string? DeletedBy { get; set; }
    }
}
