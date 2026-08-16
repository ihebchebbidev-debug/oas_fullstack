using Microsoft.AspNetCore.SignalR;
using MyApi.Modules.WorkflowEngine.DTOs;
using MyApi.Modules.WorkflowEngine.Hubs;

namespace MyApi.Modules.WorkflowEngine.Services
{
    /// <summary>
    /// Implementation of workflow notification service using SignalR.
    /// Broadcasts workflow execution events to connected clients.
    /// </summary>
    public class WorkflowNotificationService : IWorkflowNotificationService
    {
        private readonly IHubContext<WorkflowHub> _hubContext;
        private readonly ILogger<WorkflowNotificationService> _logger;

        public WorkflowNotificationService(
            IHubContext<WorkflowHub> hubContext,
            ILogger<WorkflowNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyExecutionStartedAsync(
            int workflowId, 
            int executionId, 
            string entityType, 
            int entityId,
            string? triggeredBy = null)
        {
            try
            {
                var dto = new WorkflowExecutionStartedDto
                {
                    WorkflowId = workflowId,
                    ExecutionId = executionId,
                    EntityType = entityType,
                    EntityId = entityId,
                    StartedAt = DateTime.UtcNow,
                    TriggeredBy = triggeredBy
                };

                // Broadcast to specific workflow group and all-workflows group
                await Task.WhenAll(
                    _hubContext.Clients.Group($"workflow-{workflowId}")
                        .SendAsync("ExecutionStarted", dto),
                    _hubContext.Clients.Group("all-workflows")
                        .SendAsync("ExecutionStarted", dto)
                );

                _logger.LogDebug("Notified ExecutionStarted for workflow {WorkflowId}, execution {ExecutionId}", 
                    workflowId, executionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify ExecutionStarted for workflow {WorkflowId}", workflowId);
                // Don't throw - notifications are non-critical
            }
        }

        public async Task NotifyNodeExecutingAsync(
            int workflowId, 
            int executionId, 
            string nodeId, 
            string nodeType)
        {
            try
            {
                var dto = new WorkflowNodeExecutingDto
                {
                    WorkflowId = workflowId,
                    ExecutionId = executionId,
                    NodeId = nodeId,
                    NodeType = nodeType,
                    Timestamp = DateTime.UtcNow
                };

                await Task.WhenAll(
                    _hubContext.Clients.Group($"workflow-{workflowId}")
                        .SendAsync("NodeExecuting", dto),
                    _hubContext.Clients.Group("all-workflows")
                        .SendAsync("NodeExecuting", dto)
                );

                _logger.LogDebug("Notified NodeExecuting for node {NodeId} in workflow {WorkflowId}", 
                    nodeId, workflowId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify NodeExecuting for node {NodeId}", nodeId);
            }
        }

        public async Task NotifyNodeCompletedAsync(
            int workflowId, 
            int executionId, 
            string nodeId,
            string nodeType,
            bool success, 
            string? error = null,
            string? output = null)
        {
            try
            {
                var dto = new WorkflowNodeCompletedDto
                {
                    WorkflowId = workflowId,
                    ExecutionId = executionId,
                    NodeId = nodeId,
                    NodeType = nodeType,
                    Success = success,
                    Error = error,
                    Output = output,
                    Timestamp = DateTime.UtcNow
                };

                await Task.WhenAll(
                    _hubContext.Clients.Group($"workflow-{workflowId}")
                        .SendAsync("NodeCompleted", dto),
                    _hubContext.Clients.Group("all-workflows")
                        .SendAsync("NodeCompleted", dto)
                );

                _logger.LogDebug("Notified NodeCompleted for node {NodeId} in workflow {WorkflowId}, success: {Success}", 
                    nodeId, workflowId, success);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify NodeCompleted for node {NodeId}", nodeId);
            }
        }

        public async Task NotifyExecutionCompletedAsync(
            int workflowId, 
            int executionId, 
            string status,
            int nodesExecuted = 0,
            int nodesFailed = 0)
        {
            try
            {
                var dto = new WorkflowExecutionCompletedDto
                {
                    WorkflowId = workflowId,
                    ExecutionId = executionId,
                    Status = status,
                    CompletedAt = DateTime.UtcNow,
                    NodesExecuted = nodesExecuted,
                    NodesFailed = nodesFailed
                };

                await Task.WhenAll(
                    _hubContext.Clients.Group($"workflow-{workflowId}")
                        .SendAsync("ExecutionCompleted", dto),
                    _hubContext.Clients.Group("all-workflows")
                        .SendAsync("ExecutionCompleted", dto)
                );

                _logger.LogDebug("Notified ExecutionCompleted for workflow {WorkflowId}, status: {Status}", 
                    workflowId, status);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify ExecutionCompleted for workflow {WorkflowId}", workflowId);
            }
        }

        public async Task NotifyExecutionErrorAsync(
            int workflowId, 
            int executionId, 
            string nodeId, 
            string error)
        {
            try
            {
                var dto = new WorkflowExecutionErrorDto
                {
                    WorkflowId = workflowId,
                    ExecutionId = executionId,
                    NodeId = nodeId,
                    Error = error,
                    Timestamp = DateTime.UtcNow
                };

                await Task.WhenAll(
                    _hubContext.Clients.Group($"workflow-{workflowId}")
                        .SendAsync("ExecutionError", dto),
                    _hubContext.Clients.Group("all-workflows")
                        .SendAsync("ExecutionError", dto)
                );

                _logger.LogDebug("Notified ExecutionError for workflow {WorkflowId}, node {NodeId}", 
                    workflowId, nodeId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify ExecutionError for workflow {WorkflowId}", workflowId);
            }
        }
    }
}
