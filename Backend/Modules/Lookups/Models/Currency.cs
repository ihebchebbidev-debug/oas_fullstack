using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Lookups.Models
{
    [ModuleScope("lookups")]
    [Table("Currencies")]
    public class Currency : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(3)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Symbol { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public bool IsDefault { get; set; } = false;

        public int SortOrder { get; set; } = 0;

        [MaxLength(100)]
        public string? CreatedUser { get; set; } = "system";

        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
    }
}
