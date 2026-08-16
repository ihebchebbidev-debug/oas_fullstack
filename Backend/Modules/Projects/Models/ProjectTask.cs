using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Projects.Models
{
    /// <summary>
    /// ProjectTask refactored to represent a lightweight Activity:
    /// Id, Title, Description, DueDate, AssignedUserId, TaskType, Status, RelatedEntityType, RelatedEntityId, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
    /// </summary>
    [ModuleScope("projects")]
    public class ProjectTask : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime? DueDate { get; set; }

        public int? AssignedUserId { get; set; }

        // New Activity Fields
        [Required]
        [StringLength(50)]
        public string TaskType { get; set; } = "follow-up"; // call, visit, meeting, follow-up

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "open"; // open, in progress, completed, cancelled

        [StringLength(50)]
        public string? RelatedEntityType { get; set; } // project, service_order, company, etc.

        public int? RelatedEntityId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        // Navigation properties
        [ForeignKey("AssignedUserId")]
        public virtual MyApi.Modules.Users.Models.User? AssignedUser { get; set; }
    }
}
