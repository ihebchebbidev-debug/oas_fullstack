using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Installations.Models
{
    [ModuleScope("installations")]
    [Table("InstallationNotes")]
    public class InstallationNote : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int InstallationId { get; set; }

        [Required]
        [Column(TypeName = "text")]
        public string Note { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        // Navigation property
        [ForeignKey("InstallationId")]
        public virtual Installation? Installation { get; set; }
    }
}
