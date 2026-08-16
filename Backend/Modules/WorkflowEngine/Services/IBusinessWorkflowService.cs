namespace MyApi.Modules.WorkflowEngine.Services
{
    /// <summary>
    /// Service for handling core business workflow logic.
    /// Manages the cascade of status changes across entities:
    /// Offer → Sale → Service Order → Dispatch
    /// </summary>
    public interface IBusinessWorkflowService
    {
        /// <summary>
        /// Called when an offer status changes to 'accepted'.
        /// Creates a sale automatically from the offer.
        /// </summary>
        Task<int?> HandleOfferAcceptedAsync(int offerId, string? userId = null);

        /// <summary>
        /// Called when a sale status changes to 'in_progress'.
        /// Creates service orders if the sale contains service items.
        /// </summary>
        /// <param name="saleId">The sale ID</param>
        /// <param name="userId">The user who initiated the action</param>
        /// <param name="serviceOrderConfig">Optional configuration for the service order (priority, dates, installations)</param>
        Task<int?> HandleSaleInProgressAsync(int saleId, string? userId = null, object? serviceOrderConfig = null);

        /// <summary>
        /// Called when a service order is scheduled.
        /// Creates dispatch entries for each service in the order.
        /// </summary>
        Task<IEnumerable<int>> HandleServiceOrderScheduledAsync(int serviceOrderId, string? userId = null);

        /// <summary>
        /// Called when a dispatch status changes to 'in_progress'.
        /// Updates the parent service order to 'in_progress' if not already.
        /// </summary>
        Task HandleDispatchInProgressAsync(int dispatchId, string? userId = null);

        /// <summary>
        /// Called when a dispatch status changes to 'technically_completed'.
        /// Evaluates all sibling dispatches and updates service order status:
        /// - All completed → Service Order = technically_completed
        /// - Some completed → Service Order = partially_completed
        /// </summary>
        Task HandleDispatchTechnicallyCompletedAsync(int dispatchId, string? userId = null);

        /// <summary>
        /// Gets the current workflow status summary for an entity.
        /// </summary>
        Task<WorkflowStatusSummary> GetWorkflowStatusSummaryAsync(string entityType, int entityId);
    }

    /// <summary>
    /// Summary of workflow status for an entity.
    /// </summary>
    public class WorkflowStatusSummary
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string CurrentStatus { get; set; } = string.Empty;
        public List<WorkflowStatusNote> Notes { get; set; } = new();
        public List<RelatedEntityStatus> RelatedEntities { get; set; } = new();
    }

    /// <summary>
    /// A note/log entry for workflow status changes.
    /// </summary>
    public class WorkflowStatusNote
    {
        public DateTime Timestamp { get; set; }
        public string FromStatus { get; set; } = string.Empty;
        public string ToStatus { get; set; } = string.Empty;
        public string? ChangedBy { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Status of a related entity in the workflow chain.
    /// </summary>
    public class RelatedEntityStatus
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty; // 'parent', 'child', 'sibling'
    }
}
