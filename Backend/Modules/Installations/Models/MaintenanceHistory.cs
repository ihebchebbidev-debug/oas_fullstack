using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Installations.Models
{
    [ModuleScope("installations")]
    [Table("MaintenanceHistory")]
    public class MaintenanceHistory : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int InstallationId { get; set; }

        public DateTime MaintenanceDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string MaintenanceType { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? Description { get; set; }

        [Required]
        [MaxLength(100)]
        public string PerformedBy { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Cost { get; set; }

        public DateTime? NextMaintenanceDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual Installation? Installation { get; set; }
    }
}
