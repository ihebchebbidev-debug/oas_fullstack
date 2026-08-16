using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Contacts.Models
{
    [ModuleScope("contacts")]
    [Table("ContactTags")]
    public class ContactTag : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(7)]
        public string? Color { get; set; } = "#3b82f6"; // Default blue color

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        // Navigation properties
        public virtual ICollection<ContactTagAssignment> ContactAssignments { get; set; } = new List<ContactTagAssignment>();
    }
}
