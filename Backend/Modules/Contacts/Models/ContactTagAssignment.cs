using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Contacts.Models
{
    [ModuleScope("contacts")]
    [Table("ContactTagAssignments")]
    public class ContactTagAssignment : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ContactId { get; set; }

        [Required]
        public int TagId { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? AssignedBy { get; set; }

        // Navigation properties
        [ForeignKey("ContactId")]
        public virtual Contact? Contact { get; set; }

        [ForeignKey("TagId")]
        public virtual ContactTag? Tag { get; set; }
    }
}
