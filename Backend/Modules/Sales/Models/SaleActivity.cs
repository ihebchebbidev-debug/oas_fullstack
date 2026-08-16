using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Sales.Models
{
    [ModuleScope("sales")]
    [Table("SaleActivities")]
    public class SaleActivity : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("SaleId")]
        public int SaleId { get; set; }

        [Required]
        [Column("ActivityType")]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [Column("Description")]
        public string? Description { get; set; }

        [Required]
        [Column("ActivityDate")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("PerformedBy")]
        [MaxLength(100)]
        public string CreatedByName { get; set; } = string.Empty;

        // Navigation Property
        [ForeignKey("SaleId")]
        public virtual Sale? Sale { get; set; }
    }
}
