using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.Contacts.Models;

namespace MyApi.Modules.Projects.Models
{
    [ModuleScope("projects")]
    [Table("Projects")]
    public class Project : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? Description { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "active";

        /// <summary>
        /// Distinguishes a client-success project (contact-driven container for offers/deals)
        /// from an internal/operational project. Defaults to "client".
        /// Values: "client" | "internal".
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string ProjectKind { get; set; } = "client";

        [MaxLength(20)]
        public string? Priority { get; set; }

        /// <summary>Planned budget for the project (e.g. seeded from a converted deal's value).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Budget { get; set; }

        public int? ContactId { get; set; }

        // Stored as JSON array in DB (e.g. "[1,2,3]")
        [MaxLength(1000)]
        public string? TeamMembers { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        // Navigation properties
        // ProjectTask has no ProjectId FK (refactored to entity-agnostic Activity using
        // RelatedEntityType/RelatedEntityId). The collection is kept for API compatibility
        // but must NOT be mapped by EF — otherwise EF creates a shadow ProjectId column
        // that breaks every Project save (e.g. team-member add, settings update).
        [NotMapped]
        public virtual ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
        public virtual Contact? Contact { get; set; }
    }
}
