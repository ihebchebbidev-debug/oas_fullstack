using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.WorkflowEngine.Models;
using System.Text.Json;

namespace MyApi.Modules.WorkflowEngine.Services
{
    public class WorkflowTriggerService : IWorkflowTriggerService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<WorkflowTriggerService> _logger;
        private readonly IWorkflowNotificationService _notificationService;
        private readonly IServiceProvider _serviceProvider;

        public WorkflowTriggerService(
            ApplicationDbContext db, 
            ILogger<WorkflowTriggerService> logger,
            IWorkflowNotificationService notificationService,
            IServiceProvider serviceProvider)
        {
            _db = db;
            _logger = logger;
            _notificationService = notificationService;
            _serviceProvider = serviceProvider;
        }

        public async Task TriggerStatusChangeAsync(
            string entityType,
            int entityId,
            string oldStatus,
            string newStatus,
            string? userId = null,
            object? context = null)
        {
            _logger.LogInformation(
                "[WORKFLOW-TRIGGER] Status change detected: {EntityType} #{EntityId} from '{OldStatus}' to '{NewStatus}' (User: {UserId})",
                entityType, entityId, oldStatus, newStatus, userId ?? "system");

            // Log all registered triggers for this entity type for debugging
            var allTriggersForEntity = await _db.WorkflowTriggers
                .Include(t => t.Workflow)
                .Where(t => t.EntityType == entityType && t.IsActive)
                .ToListAsync();
            
            _logger.LogInformation(
                "[WORKFLOW-TRIGGER] Found {Count} registered triggers for {EntityType}. Checking matches...",
                allTriggersForEntity.Count, entityType);
            
            foreach (var t in allTriggersForEntity)
            {
                var fromMatch = t.FromStatus == null || t.FromStatus == oldStatus;
                var toMatch = t.ToStatus == null || t.ToStatus == newStatus;
                var workflowActive = t.Workflow == null || (t.Workflow.IsActive && !t.Workflow.IsDeleted);
                
                _logger.LogInformation(
                    "[WORKFLOW-TRIGGER] Trigger #{TriggerId} (Node: {NodeId}): FromStatus={From} (match: {FromMatch}), ToStatus={To} (match: {ToMatch}), WorkflowActive: {WorkflowActive}",
                    t.Id, t.NodeId, t.FromStatus ?? "ANY", fromMatch, t.ToStatus ?? "ANY", toMatch, workflowActive);
            }

            // Find matching triggers
            var triggers = await _db.WorkflowTriggers
                .Include(t => t.Workflow)
                .Where(t => t.IsActive 
                    && t.EntityType == entityType
                    && (t.Workflow == null || (t.Workflow.IsActive && !t.Workflow.IsDeleted))
                    && (t.FromStatus == null || t.FromStatus == oldStatus)
                    && (t.ToStatus == null || t.ToStatus == newStatus))
                .ToListAsync();

            if (!triggers.Any())
            {
                _logger.LogWarning(
                    "[WORKFLOW-TRIGGER] NO MATCHING triggers for {EntityType} #{EntityId}: '{OldStatus}' -> '{NewStatus}'. " +
                    "Check if trigger is registered with correct fromStatus/toStatus values.",
                    entityType, entityId, oldStatus, newStatus);
                return;
            }
            
            _logger.LogInformation(
                "[WORKFLOW-TRIGGER] {Count} triggers MATCHED for {EntityType} #{EntityId}",
                triggers.Count, entityType, entityId);

            _logger.LogInformation("Found {Count} matching triggers for {EntityType} {EntityId}", 
                triggers.Count, entityType, entityId);

            // Create execution for each matching workflow
            foreach (var trigger in triggers)
            {
                // RELIABILITY (BUG-1): skip if there is already an in-flight execution
                // for the same (workflow, entity). Rapid back-to-back status changes
                // could otherwise spawn duplicate parallel executions that fight each
                // other (e.g. both create a child offer, both send the same email).
                var alreadyRunning = await _db.WorkflowExecutions.AnyAsync(e =>
                    e.WorkflowId == trigger.WorkflowId &&
                    e.TriggerEntityType == entityType &&
                    e.TriggerEntityId == entityId &&
                    (e.Status == "running" || e.Status == "waiting_approval" || e.Status == "waiting_delay"));
                if (alreadyRunning)
                {
                    _logger.LogInformation(
                        "[WORKFLOW-TRIGGER] Skipping duplicate execution for workflow {WorkflowId} on {EntityType} #{EntityId} — an execution is already in-flight",
                        trigger.WorkflowId, entityType, entityId);
                    continue;
                }

                WorkflowExecution? execution = null;
                try
                {
                    execution = new WorkflowExecution
                    {
                        WorkflowId = trigger.WorkflowId,
                        TriggerEntityType = entityType,
                        TriggerEntityId = entityId,
                        Status = "running",
                        CurrentNodeId = trigger.NodeId,
                        Context = JsonSerializer.Serialize(new
                        {
                            entityType,
                            entityId,
                            oldStatus,
                            newStatus,
                            triggeredAt = DateTime.UtcNow,
                            additionalContext = context
                        }),
                        StartedAt = DateTime.UtcNow,
                        TriggeredBy = userId
                    };

                    _db.WorkflowExecutions.Add(execution);
                    await _db.SaveChangesAsync();

                    // Notify clients that execution started
                    await _notificationService.NotifyExecutionStartedAsync(
                        trigger.WorkflowId, 
                        execution.Id, 
                        entityType, 
                        entityId, 
                        userId);

                    // Notify that trigger node is executing
                    await _notificationService.NotifyNodeExecutingAsync(
                        trigger.WorkflowId,
                        execution.Id,
                        trigger.NodeId,
                        "status-trigger");

                    // Log the trigger start
                    var triggerLog = new WorkflowExecutionLog
                    {
                        ExecutionId = execution.Id,
                        NodeId = trigger.NodeId,
                        NodeType = "status-trigger",
                        Status = "completed",
                        Input = JsonSerializer.Serialize(new { oldStatus, newStatus }),
                        Output = JsonSerializer.Serialize(new { triggered = true }),
                        Timestamp = DateTime.UtcNow
                    };

                    _db.WorkflowExecutionLogs.Add(triggerLog);
                    await _db.SaveChangesAsync();

                    // Notify that trigger node completed
                    await _notificationService.NotifyNodeCompletedAsync(
                        trigger.WorkflowId,
                        execution.Id,
                        trigger.NodeId,
                        "status-trigger",
                        true,
                        null,
                        JsonSerializer.Serialize(new { triggered = true }));

                    _logger.LogInformation(
                        "Created workflow execution {ExecutionId} for workflow {WorkflowId}",
                        execution.Id, trigger.WorkflowId);

                    // Execute the entire workflow graph
                    var executionContext = new WorkflowExecutionContext
                    {
                        WorkflowId = trigger.WorkflowId,
                        ExecutionId = execution.Id,
                        TriggerEntityType = entityType,
                        TriggerEntityId = entityId,
                        UserId = userId,
                        Variables = new Dictionary<string, object?>
                        {
                            ["oldStatus"] = oldStatus,
                            ["newStatus"] = newStatus,
                            ["entityId"] = entityId,
                            ["entityType"] = entityType,
                            ["additionalContext"] = context // Pass the full context including serviceOrderConfig
                        }
                    };

                    // Resolve the graph executor from service provider to avoid circular dependency
                    var graphExecutor = _serviceProvider.GetRequiredService<IWorkflowGraphExecutor>();
                    var graphResult = await graphExecutor.ExecuteGraphAsync(
                        trigger.WorkflowId,
                        execution.Id,
                        trigger.NodeId,
                        executionContext);

                    // Update execution status based on graph result
                    execution.Status = graphResult.FinalStatus;
                    execution.Error = graphResult.Error;
                    
                    if (graphResult.FinalStatus == "completed" || graphResult.FinalStatus == "failed")
                    {
                        execution.CompletedAt = DateTime.UtcNow;
                    }
                    
                    await _db.SaveChangesAsync();

                    // Notify that execution completed
                    await _notificationService.NotifyExecutionCompletedAsync(
                        trigger.WorkflowId,
                        execution.Id,
                        graphResult.FinalStatus,
                        graphResult.NodesExecuted,
                        graphResult.NodesFailed);

                    _logger.LogInformation(
                        "Workflow execution {ExecutionId} completed with status {Status}. Nodes: {Executed} executed, {Failed} failed, {Skipped} skipped",
                        execution.Id, graphResult.FinalStatus, graphResult.NodesExecuted, graphResult.NodesFailed, graphResult.NodesSkipped);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error executing workflow {WorkflowId} for trigger {TriggerId}", 
                        trigger.WorkflowId, trigger.Id);

                    // BUG FIX: previously the execution row was never updated, leaving it
                    // stuck in "running" until manual cleanup; the notification also
                    // reported executionId=0 which clients cannot map to a real row.
                    var executionId = execution?.Id ?? 0;
                    if (execution != null)
                    {
                        // BUG FIX: this used to call _db.ChangeTracker.Clear() on the
                        // *caller's* request-scoped context (this service runs inline
                        // inside DealService/OfferService/... SaveChanges flows), which
                        // detached every pending entity the caller still meant to save.
                        // Write the failure through an isolated scope instead.
                        await MarkExecutionFailedAsync(execution.Id, ex.Message);
                    }


                    await _notificationService.NotifyExecutionErrorAsync(
                        trigger.WorkflowId,
                        executionId,
                        trigger.NodeId,
                        ex.Message);
                }
            }
        }

        public async Task<int?> TriggerWebhookAsync(
            string path,
            string? token,
            JsonElement payload,
            string? userId = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.LogWarning("[WORKFLOW-WEBHOOK] Empty path on inbound webhook");
                return null;
            }

            // Webhook triggers are stored as EntityType="webhook", FromStatus=path,
            // ToStatus=optional secret token. This reuses the existing schema, no migration.
            var trigger = await _db.WorkflowTriggers
                .Include(t => t.Workflow)
                .FirstOrDefaultAsync(t => t.IsActive
                    && t.EntityType == "webhook"
                    && t.FromStatus == path
                    && (t.Workflow == null || (t.Workflow.IsActive && !t.Workflow.IsDeleted)));

            if (trigger == null)
            {
                _logger.LogWarning("[WORKFLOW-WEBHOOK] No active trigger for path '{Path}'", path);
                return null;
            }

            // Token validation: if the trigger has a token configured (ToStatus), require it.
            if (!string.IsNullOrEmpty(trigger.ToStatus))
            {
                if (string.IsNullOrEmpty(token) || !string.Equals(trigger.ToStatus, token, StringComparison.Ordinal))
                {
                    _logger.LogWarning("[WORKFLOW-WEBHOOK] Invalid token for webhook path '{Path}'", path);
                    return null;
                }
            }

            // Concurrency guard: a webhook can legitimately fire many times, so we don't
            // dedupe by entity; we still dedupe by (workflow, path) within the last 2 seconds
            // to swallow accidental double-deliveries.
            var since = DateTime.UtcNow.AddSeconds(-2);
            var recentDuplicate = await _db.WorkflowExecutions.AnyAsync(e =>
                e.WorkflowId == trigger.WorkflowId &&
                e.TriggerEntityType == "webhook" &&
                e.StartedAt >= since);
            if (recentDuplicate)
            {
                _logger.LogInformation("[WORKFLOW-WEBHOOK] Duplicate inbound webhook for workflow {WorkflowId} within debounce window; skipping", trigger.WorkflowId);
                return null;
            }

            var payloadJson = payload.ValueKind == JsonValueKind.Undefined ? "{}" : payload.GetRawText();
            WorkflowExecution? execution = null;
            try
            {
                execution = new WorkflowExecution
                {
                    WorkflowId = trigger.WorkflowId,
                    TriggerEntityType = "webhook",
                    TriggerEntityId = 0,
                    Status = "running",
                    CurrentNodeId = trigger.NodeId,
                    Context = JsonSerializer.Serialize(new
                    {
                        source = "webhook",
                        path,
                        triggeredAt = DateTime.UtcNow,
                        payload = payloadJson
                    }),
                    StartedAt = DateTime.UtcNow,
                    TriggeredBy = userId
                };
                _db.WorkflowExecutions.Add(execution);
                await _db.SaveChangesAsync();

                await _notificationService.NotifyExecutionStartedAsync(
                    trigger.WorkflowId, execution.Id, "webhook", 0, userId);
                await _notificationService.NotifyNodeExecutingAsync(
                    trigger.WorkflowId, execution.Id, trigger.NodeId, "webhook-trigger");

                _db.WorkflowExecutionLogs.Add(new WorkflowExecutionLog
                {
                    ExecutionId = execution.Id,
                    NodeId = trigger.NodeId,
                    NodeType = "webhook-trigger",
                    Status = "completed",
                    Input = payloadJson.Length > 8000 ? payloadJson.Substring(0, 8000) : payloadJson,
                    Output = JsonSerializer.Serialize(new { triggered = true, path }),
                    Timestamp = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                await _notificationService.NotifyNodeCompletedAsync(
                    trigger.WorkflowId, execution.Id, trigger.NodeId, "webhook-trigger", true, null,
                    JsonSerializer.Serialize(new { triggered = true }));

                var execContext = new WorkflowExecutionContext
                {
                    WorkflowId = trigger.WorkflowId,
                    ExecutionId = execution.Id,
                    TriggerEntityType = "webhook",
                    TriggerEntityId = 0,
                    UserId = userId,
                    Variables = new Dictionary<string, object?>
                    {
                        ["source"] = "webhook",
                        ["webhookPath"] = path,
                        ["payload"] = payloadJson
                    }
                };

                var graphExecutor = _serviceProvider.GetRequiredService<IWorkflowGraphExecutor>();
                var graphResult = await graphExecutor.ExecuteGraphAsync(
                    trigger.WorkflowId, execution.Id, trigger.NodeId, execContext);

                execution.Status = graphResult.FinalStatus;
                execution.Error = graphResult.Error;
                if (graphResult.FinalStatus == "completed" || graphResult.FinalStatus == "failed")
                {
                    execution.CompletedAt = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync();

                await _notificationService.NotifyExecutionCompletedAsync(
                    trigger.WorkflowId, execution.Id, graphResult.FinalStatus,
                    graphResult.NodesExecuted, graphResult.NodesFailed);

                return execution.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW-WEBHOOK] Error executing webhook workflow {WorkflowId}", trigger.WorkflowId);
                if (execution != null)
                {
                    // Same fix as TriggerStatusChangeAsync: never clear the caller's
                    // change tracker — use an isolated scope for the failure write.
                    await MarkExecutionFailedAsync(execution.Id, ex.Message);
                }
                return execution?.Id;
            }
        }

        /// <summary>
        /// Marks an execution row as failed using a dedicated DI scope + DbContext.
        /// Runs in isolation so it can never disturb the change tracker of the request
        /// that happens to be hosting this trigger call.
        /// </summary>
        private async Task MarkExecutionFailedAsync(int executionId, string error)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var fresh = await db.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == executionId);
                if (fresh != null)
                {
                    fresh.Status = "failed";
                    fresh.Error = error.Length > 1000 ? error.Substring(0, 1000) : error;
                    fresh.CompletedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to mark execution {ExecutionId} as failed after trigger error", executionId);
            }
        }



        public async Task<int> GetPendingExecutionsCountAsync(string entityType, int entityId)
        {
            return await _db.WorkflowExecutions
                .CountAsync(e => e.TriggerEntityType == entityType 
                    && e.TriggerEntityId == entityId 
                    && (e.Status == "running" || e.Status == "waiting_approval"));
        }

        public async Task<IEnumerable<WorkflowTriggerInfo>> GetActiveTriggersAsync(string entityType)
        {
            var triggers = await _db.WorkflowTriggers
                .Include(t => t.Workflow)
                .Where(t => t.IsActive 
                    && t.EntityType == entityType
                    && t.Workflow != null 
                    && t.Workflow.IsActive 
                    && !t.Workflow.IsDeleted)
                .ToListAsync();

            return triggers.Select(t => new WorkflowTriggerInfo
            {
                TriggerId = t.Id,
                WorkflowId = t.WorkflowId,
                WorkflowName = t.Workflow?.Name ?? "",
                EntityType = t.EntityType,
                FromStatus = t.FromStatus,
                ToStatus = t.ToStatus,
                IsActive = t.IsActive
            });
        }
    }
}
