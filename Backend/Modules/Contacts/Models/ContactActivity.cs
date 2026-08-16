using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Contacts.Models
{
    /// <summary>
    /// System-generated timeline entry for a contact. Records automatic events
    /// (offer / sale / service order / dispatch / installation lifecycle) as well
    /// as manual notes so the "Activity" tab on the contact detail page can render
    /// a unified chronological feed.
    /// </summary>
    [ModuleScope("contacts")]
    [Table("ContactActivities")]
    public class ContactActivity : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ContactId { get; set; }

        /// <summary>
        /// Machine-readable activity type used by the frontend for i18n / iconography.
        /// See <see cref="ContactActivityTypes"/> for the canonical values.
        /// </summary>
        [Required]
        [MaxLength(60)]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Related entity type (Offer, Sale, ServiceOrder, Dispatch, Installation, Note).
        /// </summary>
        [MaxLength(40)]
        public string? RelatedEntityType { get; set; }

        public int? RelatedEntityId { get; set; }

        /// <summary>
        /// Short human-readable label (English fallback); the frontend prefers to
        /// re-translate from Type + Metadata but falls back to this string.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// JSON metadata (e.g. { "number": "OFF-2024-001", "amount": 1200, "status": "won" }).
        /// </summary>
        [Column(TypeName = "text")]
        public string? Metadata { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [ForeignKey("ContactId")]
        public virtual Contact? Contact { get; set; }
    }

    public static class ContactActivityTypes
    {
        public const string NoteAdded = "note_added";
        public const string NoteUpdated = "note_updated";
        public const string NoteDeleted = "note_deleted";

        public const string OfferCreated = "offer_created";
        public const string OfferStatusChanged = "offer_status_changed";

        public const string SaleCreated = "sale_created";
        public const string SaleStatusChanged = "sale_status_changed";

        public const string ServiceOrderCreated = "service_order_created";
        public const string ServiceOrderStatusChanged = "service_order_status_changed";

        public const string DispatchCreated = "dispatch_created";
        public const string DispatchStatusChanged = "dispatch_status_changed";

        public const string InstallationCreated = "installation_created";
        public const string InstallationCompleted = "installation_completed";

        public const string ContactUpdated = "contact_updated";

        // Planned time / expense / material logged against an offer item, sale item, or service order job.
        public const string PlannedEntryAdded = "planned_entry_added";
        public const string PlannedEntryUpdated = "planned_entry_updated";
        public const string PlannedEntryDeleted = "planned_entry_deleted";
    }

    public static class ContactActivityEntityTypes
    {
        public const string Offer = "Offer";
        public const string Sale = "Sale";
        public const string ServiceOrder = "ServiceOrder";
        public const string Dispatch = "Dispatch";
        public const string Installation = "Installation";
        public const string Note = "Note";
        public const string PlannedEntry = "PlannedEntry";
    }
}
