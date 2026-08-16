using System;
using System.Threading.Tasks;

namespace MyApi.Modules.Shared.Services
{
    /// <summary>
    /// Uniform activity/audit logger.
    ///
    /// Writes every mutation to TWO destinations:
    ///   1. <see cref="ISystemLogService"/> (the flat cross-module audit stream) — always.
    ///   2. The per-entity Activity table of the parent (OfferActivities /
    ///      SaleActivities / DealActivities / ProjectActivities) — when the
    ///      caller supplies a parent entity type/id. This is what powers the
    ///      "Activity" tab on offer/sale/deal/project detail pages, so mutations
    ///      on child rows (items, materials, expenses, documents, notes)
    ///      surface on the parent's timeline.
    ///
    /// Callers should prefer <see cref="LogAsync"/> over calling
    /// <see cref="ISystemLogService"/> directly so nothing silently escapes the
    /// per-entity Activity tab.
    /// </summary>
    public interface IActivityLogger
    {
        Task LogAsync(ActivityLogEntry entry);
    }

    public sealed class ActivityLogEntry
    {
        /// <summary>e.g. "Offers", "Sales", "Deals", "Projects", "ServiceOrders", "Dispatches", "Documents".</summary>
        public string Module { get; init; } = "other";

        /// <summary>
        /// Verb tag (create/update/delete/status_change/item_added/item_updated/
        /// item_deleted/material_added/material_approved/expense_added/
        /// time_entry_added/document_attached/converted/note_added/assigned/…).
        /// </summary>
        public string Action { get; init; } = "other";

        /// <summary>
        /// Type of the entity being mutated ("Offer", "OfferItem", "Document", …).
        /// Used to key the SystemLog row and — when the type is one of the four
        /// top-level entities (Offer/Sale/Deal/Project) — also to insert a row
        /// into that entity's own Activity table without needing a parent hint.
        /// </summary>
        public string EntityType { get; init; } = string.Empty;

        public string EntityId { get; init; } = string.Empty;

        /// <summary>Short human-readable summary shown in the timeline.</summary>
        public string Message { get; init; } = string.Empty;

        public string? UserId { get; init; }
        public string? UserName { get; init; }

        /// <summary>Optional detail JSON/string — surfaced in SystemLog `Details`.</summary>
        public string? Details { get; init; }

        /// <summary>
        /// Parent entity for child mutations. When set, an activity row is
        /// written to the parent's per-module activity table so the parent's
        /// timeline shows the child mutation (e.g. Item added on Offer #123).
        /// One of: "Offer" | "Sale" | "Deal" | "Project".
        /// </summary>
        public string? ParentEntityType { get; init; }
        public int? ParentEntityId { get; init; }

        // For DealActivity old/new value tracking.
        public string? OldValue { get; init; }
        public string? NewValue { get; init; }
    }
}
