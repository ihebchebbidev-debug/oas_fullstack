using Microsoft.AspNetCore.SignalR;

namespace MyApi.Modules.WorkflowEngine.Hubs
{
    /// <summary>
    /// SignalR hub for real-time workflow execution updates.
    /// Clients can join specific workflow groups to receive targeted notifications.
    /// </summary>
    public class WorkflowHub : Hub
    {
        private readonly ILogger<WorkflowHub> _logger;

        public WorkflowHub(ILogger<WorkflowHub> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Join a specific workflow's notification group.
        /// </summary>
        public async Task JoinWorkflowGroup(int workflowId)
        {
            var groupName = $"workflow-{workflowId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogDebug("Client {ConnectionId} joined workflow group {GroupName}", 
                Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Leave a specific workflow's notification group.
        /// </summary>
        public async Task LeaveWorkflowGroup(int workflowId)
        {
            var groupName = $"workflow-{workflowId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogDebug("Client {ConnectionId} left workflow group {GroupName}", 
                Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Join the "all-workflows" group to receive all workflow notifications.
        /// Useful for dashboard views that monitor all workflows.
        /// </summary>
        public async Task JoinAllWorkflows()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "all-workflows");
            _logger.LogDebug("Client {ConnectionId} joined all-workflows group", Context.ConnectionId);
        }

        /// <summary>
        /// Leave the "all-workflows" group.
        /// </summary>
        public async Task LeaveAllWorkflows()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all-workflows");
            _logger.LogDebug("Client {ConnectionId} left all-workflows group", Context.ConnectionId);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
