using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Lookups.Models
{
    [ModuleScope("lookups")]
    [Table("LookupItems")]
    public class LookupItem : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(100)]
        public string? Value { get; set; }

        public int? DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        [MaxLength(50)]
        public string? LookupType { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? Color { get; set; }

        public int SortOrder { get; set; } = 0;

        [MaxLength(100)]
        public string? CreatedUser { get; set; } = "system";

        [MaxLength(100)]
        public string? ModifyUser { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDefault { get; set; } = false;
        // Indicates whether this lookup (used for leave types) is paid
        public bool? IsPaid { get; set; } = null;
    }
}
