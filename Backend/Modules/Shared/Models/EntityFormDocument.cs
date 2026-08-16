using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.DynamicForms.Models;
using MyApi.Modules.Shared.Domain.Common;

namespace MyApi.Modules.Shared.Models
{
    /// <summary>
    /// Status for entity form documents
    /// </summary>
    public enum FormDocumentStatus
    {
        Draft,
        Completed
    }

    /// <summary>
    /// Entity Form Document - links dynamic forms to offers/sales
    /// Allows multiple form documents per entity, same form can be filled multiple times
    /// </summary>
    [ModuleScope("documents")]
    [Table("EntityFormDocuments")]
    public class EntityFormDocument : BaseEntityWithSoftDelete, ITenantEntity
    {
        public int TenantId { get; set; }
        /// <summary>
        /// Type of entity: 'offer' or 'sale'
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// ID of the linked entity (Offer.Id or Sale.Id)
        /// </summary>
        [Required]
        public int EntityId { get; set; }

        /// <summary>
        /// Reference to the dynamic form template
        /// </summary>
        [Required]
        public int FormId { get; set; }

        /// <summary>
        /// Version of the form at time of creation (snapshot)
        /// </summary>
        [Required]
        public int FormVersion { get; set; } = 1;

        /// <summary>
        /// Optional custom title for this document instance
        /// </summary>
        [MaxLength(200)]
        public string? Title { get; set; }

        /// <summary>
        /// Document status: Draft or Completed
        /// </summary>
        [Required]
        public FormDocumentStatus Status { get; set; } = FormDocumentStatus.Draft;

        /// <summary>
        /// JSON blob containing form field responses
        /// </summary>
        [Required]
        [Column(TypeName = "jsonb")]
        public string Responses { get; set; } = "{}";

        // Navigation property
        [ForeignKey("FormId")]
        public virtual DynamicForm? Form { get; set; }
    }
}
