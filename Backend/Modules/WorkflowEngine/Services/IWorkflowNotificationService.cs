namespace MyApi.Modules.WorkflowEngine.Services
{
    /// <summary>
    /// Service for broadcasting real-time workflow execution events via SignalR.
    /// This service is additive - it does not affect workflow execution logic.
    /// If SignalR fails, workflow execution continues normally.
    /// </summary>
    public interface IWorkflowNotificationService
    {
        /// <summary>
        /// Notifies clients that a workflow execution has started.
        /// </summary>
        Task NotifyExecutionStartedAsync(
            int workflowId, 
            int executionId, 
            string entityType, 
            int entityId,
            string? triggeredBy = null);

        /// <summary>
        /// Notifies clients that a specific node is now executing.
        /// </summary>
        Task NotifyNodeExecutingAsync(
            int workflowId, 
            int executionId, 
            string nodeId, 
            string nodeType);

        /// <summary>
        /// Notifies clients that a specific node has completed execution.
        /// </summary>
        Task NotifyNodeCompletedAsync(
            int workflowId, 
            int executionId, 
            string nodeId,
            string nodeType,
            bool success, 
            string? error = null,
            string? output = null);

        /// <summary>
        /// Notifies clients that a workflow execution has completed.
        /// </summary>
        Task NotifyExecutionCompletedAsync(
            int workflowId, 
            int executionId, 
            string status,
            int nodesExecuted = 0,
            int nodesFailed = 0);

        /// <summary>
        /// Notifies clients of a workflow execution error.
        /// </summary>
        Task NotifyExecutionErrorAsync(
            int workflowId, 
            int executionId, 
            string nodeId, 
            string error);
    }
}
