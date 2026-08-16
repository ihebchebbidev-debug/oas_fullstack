using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.UserGroups.Models;

namespace MyApi.Modules.Contacts.Models
{
    [ModuleScope("contacts")]
    [Table("ContactUserGroups")]
    public class ContactUserGroupAssignment : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ContactId { get; set; }

        [Required]
        public int UserGroupId { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? AssignedBy { get; set; }

        // Navigation properties
        [ForeignKey("ContactId")]
        public virtual Contact? Contact { get; set; }

        [ForeignKey("UserGroupId")]
        public virtual UserGroup? UserGroup { get; set; }
    }
}
