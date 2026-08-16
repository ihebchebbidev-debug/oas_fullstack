using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Contacts.Models
{
    [ModuleScope("contacts")]
    [Table("ContactNotes")]
    public class ContactNote : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ContactId { get; set; }

        [Required]
        [Column(TypeName = "text")]
        public string Note { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        // Navigation property to prevent EF from creating shadow properties
        [ForeignKey("ContactId")]
        public virtual Contact? Contact { get; set; }
    }
}
