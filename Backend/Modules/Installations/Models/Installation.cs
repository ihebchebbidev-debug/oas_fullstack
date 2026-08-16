using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Installations.Models
{
    [ModuleScope("installations")]
    [Table("Installations")]
    public class Installation : ITenantEntity, MyApi.Modules.Shared.Models.ISoftDeletable
    {
        public int TenantId { get; set; }

        // Soft-delete columns (added 2026-07-25 to stop orphaned FK references
        // on TimeEntries/Expenses/MaterialUsage/ServiceOrderJobs/ServiceOrderMaterials
        // which carry a denormalized InstallationId without a real FK).
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string InstallationNumber { get; set; } = string.Empty;

        public int ContactId { get; set; }

        [Required]
        [MaxLength(500)]
        public string SiteAddress { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string InstallationType { get; set; } = string.Empty;

        public DateTime InstallationDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "active";

        public DateTime? WarrantyExpiry { get; set; }

        [Column(TypeName = "text")]
        public string? Notes { get; set; }

        // New fields for frontend compatibility
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(200)]
        public string? Model { get; set; }

        [MaxLength(200)]
        public string? Manufacturer { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        [MaxLength(100)]
        public string? SerialNumber { get; set; }

        [MaxLength(100)]
        public string? Matricule { get; set; }

        public DateTime? WarrantyFrom { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        // Navigation properties
        public virtual ICollection<MaintenanceHistory> MaintenanceHistories { get; set; } = new List<MaintenanceHistory>();
    }
}
