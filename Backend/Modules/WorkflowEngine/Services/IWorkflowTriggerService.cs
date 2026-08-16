namespace MyApi.Modules.WorkflowEngine.Services
{
    /// <summary>
    /// Service interface for triggering workflows based on entity status changes.
    /// This service is called by other entity services (Offers, Sales, ServiceOrders, Dispatches)
    /// when their status changes to find and execute matching workflows.
    /// </summary>
    public interface IWorkflowTriggerService
    {
        /// <summary>
        /// Called by entity services when a status changes.
        /// Finds matching workflows and executes them.
        /// </summary>
        /// <param name="entityType">The type of entity: 'offer', 'sale', 'service_order', 'dispatch'</param>
        /// <param name="entityId">The ID of the entity that changed</param>
        /// <param name="oldStatus">The previous status value</param>
        /// <param name="newStatus">The new status value</param>
        /// <param name="userId">The user who triggered the change (optional)</param>
        /// <param name="context">Additional context data for the workflow (optional)</param>
        Task TriggerStatusChangeAsync(
            string entityType,
            int entityId,
            string oldStatus,
            string newStatus,
            string? userId = null,
            object? context = null
        );

        /// <summary>
        /// Starts a workflow execution from an inbound HTTP webhook. Looks up an active
        /// trigger registered as EntityType="webhook" with FromStatus matching the path slug,
        /// and (if configured) validates the provided token against ToStatus.
        /// Returns the new execution id, or null if no matching trigger.
        /// </summary>
        Task<int?> TriggerWebhookAsync(
            string path,
            string? token,
            System.Text.Json.JsonElement payload,
            string? userId = null
        );

        /// <summary>
        /// Gets the count of pending workflow executions for an entity.
        /// </summary>
        Task<int> GetPendingExecutionsCountAsync(string entityType, int entityId);

        /// <summary>
        /// Gets all registered triggers for a specific entity type.
        /// </summary>
        Task<IEnumerable<WorkflowTriggerInfo>> GetActiveTriggersAsync(string entityType);
    }

    /// <summary>
    /// Information about a registered workflow trigger.
    /// </summary>
    public class WorkflowTriggerInfo
    {
        public int TriggerId { get; set; }
        public int WorkflowId { get; set; }
        public string WorkflowName { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public bool IsActive { get; set; }
    }
}
