using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.WorkflowEngine.Models;
using System.Text.Json;

namespace MyApi.Modules.WorkflowEngine.Services
{
    /// <summary>
    /// Background service that polls entity states every 5 minutes
    /// and triggers workflows based on CURRENT status (state-based),
    /// not just status transitions (event-based).
    /// </summary>
    public class WorkflowPollingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowPollingService> _logger;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(5);

        public WorkflowPollingService(
            IServiceProvider serviceProvider,
            ILogger<WorkflowPollingService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[WORKFLOW-POLLING] Starting workflow polling service. Interval: {Interval} minutes", 
                _pollingInterval.TotalMinutes);

            // Wait a bit before first run to let the app fully start
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollAndTriggerWorkflowsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WORKFLOW-POLLING] Error during polling cycle");
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }

            _logger.LogInformation("[WORKFLOW-POLLING] Workflow polling service stopped");
        }

        /// <summary>
        /// Resolve which tenants this cycle must run for.
        /// A BackgroundService has no HttpContext, so ApplicationDbContext keeps its
        /// default _currentTenantId = 0 and every workflow query would be filtered to
        /// tenant 0 only — silently skipping every real company. We therefore run one
        /// full cycle per active tenant. When the workflow_engine module is configured
        /// as "shared" all rows live at TenantId = 0, so a single pass is enough (and
        /// looping would process the same rows N times).
        /// </summary>
        private async Task<List<int>> ResolveTenantIdsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (db.IsModuleShared("workflow_engine"))
                return new List<int> { 0 };

            var ids = await db.Tenants
                .Where(t => t.IsActive)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            // Tenant 0 is the system/default bucket — always include it.
            if (!ids.Contains(0)) ids.Insert(0, 0);
            return ids;
        }

        private async Task PollAndTriggerWorkflowsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[WORKFLOW-POLLING] ═══════════════════════════════════════════════════════════════");
            _logger.LogInformation("[WORKFLOW-POLLING] Starting polling cycle at {Time}", DateTime.UtcNow);

            var tenantIds = await ResolveTenantIdsAsync(cancellationToken);
            _logger.LogInformation("[WORKFLOW-POLLING] Running cycle for {Count} tenant(s): {Tenants}",
                tenantIds.Count, string.Join(",", tenantIds));

            foreach (var tenantId in tenantIds)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    await PollTenantAsync(tenantId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // One bad tenant must never abort the whole cycle.
                    _logger.LogError(ex, "[WORKFLOW-POLLING] Cycle failed for tenant {TenantId}", tenantId);
                }
            }

            _logger.LogInformation("[WORKFLOW-POLLING] ═══════════════════════════════════════════════════════════════");
        }

        private async Task PollTenantAsync(int tenantId, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Scope every query AND every insert in this cycle to the owning tenant.
            // (Do not use the -1 "view all" sentinel here: StampTenantIdOnNewEntities
            // throws on any tenant-scoped write while it is active.)
            db.SetTenantId(tenantId);
            var notificationService = scope.ServiceProvider.GetRequiredService<IWorkflowNotificationService>();
            var graphExecutor = scope.ServiceProvider.GetRequiredService<IWorkflowGraphExecutor>();


            // ═══ PHASE 0: Purge stale execution records (TTL housekeeping) ═══
            var purged = await PurgeStaleExecutionsAsync(db, cancellationToken);
            _logger.LogInformation("[WORKFLOW-POLLING] Phase 0 - Stale executions purged: {Count}", purged);

            // ═══ PHASE 1: Direct status consistency reconciliation ═══
            // This fixes mismatches that the trigger-based system may have missed
            var consistencyFixes = await ReconcileStatusConsistencyAsync(db, cancellationToken);
            _logger.LogInformation("[WORKFLOW-POLLING] Phase 1 - Consistency fixes: {Fixes}", consistencyFixes);

            // ═══ PHASE 1.4: Expire timed-out approval requests ═══
            var expired = await ExpireTimedOutApprovalsAsync(db, graphExecutor, cancellationToken);
            _logger.LogInformation("[WORKFLOW-POLLING] Phase 1.4 - Approvals expired: {Count}", expired);

            // ═══ PHASE 1.5: Resume delayed executions whose ResumeAt has passed ═══
            var resumed = await ResumeDueDelayedExecutionsAsync(db, graphExecutor, cancellationToken);
            _logger.LogInformation("[WORKFLOW-POLLING] Phase 1.5 - Delayed executions resumed: {Count}", resumed);

            // ═══ PHASE 1.6: Fire scheduled-trigger nodes whose interval is due ═══
            var scheduled = await FireDueScheduledTriggersAsync(db, graphExecutor, cancellationToken);
            _logger.LogInformation("[WORKFLOW-POLLING] Phase 1.6 - Scheduled triggers fired: {Count}", scheduled);

            // ═══ PHASE 2: Trigger-based workflow execution (existing logic) ═══
            // Get all active triggers with their workflows
            var triggers = await db.WorkflowTriggers
                .Include(t => t.Workflow)
                .Where(t => t.IsActive 
                    && t.Workflow != null 
                    && t.Workflow.IsActive 
                    && !t.Workflow.IsDeleted)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("[WORKFLOW-POLLING] Found {Count} active triggers to check", triggers.Count);

            int totalProcessed = 0;
            int totalTriggered = 0;

            foreach (var trigger in triggers)
            {
                try
                {
                    var (processed, triggered) = await ProcessTriggerAsync(
                        db, trigger, graphExecutor, notificationService, cancellationToken);
                    totalProcessed += processed;
                    totalTriggered += triggered;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WORKFLOW-POLLING] Error processing trigger {TriggerId} (Node: {NodeId})", 
                        trigger.Id, trigger.NodeId);
                }
            }

            _logger.LogInformation("[WORKFLOW-POLLING] Tenant {TenantId} complete. Consistency fixes: {ConsistencyFixes}, Entities checked: {Processed}, Workflows triggered: {Triggered}",
                tenantId, consistencyFixes, totalProcessed, totalTriggered);
        }

        /// <summary>
        /// Delete completed/cancelled executions older than 30 days and failed ones older
        /// than 90 days, including their logs and approval records. Runs every cycle but is
        /// capped at 500 rows per pass so it never blocks the polling cycle significantly.
        /// </summary>
        private async Task<int> PurgeStaleExecutionsAsync(ApplicationDbContext db, CancellationToken ct)
        {
            try
            {
                var now = DateTime.UtcNow;
                var completedCutoff = now.AddDays(-30);
                var failedCutoff = now.AddDays(-90);

                var completedIds = await db.WorkflowExecutions
                    .Where(e => (e.Status == "completed" || e.Status == "cancelled") && e.CompletedAt < completedCutoff)
                    .Select(e => e.Id)
                    .Take(500)
                    .ToListAsync(ct);

                var failedIds = await db.WorkflowExecutions
                    .Where(e => e.Status == "failed" && e.CompletedAt < failedCutoff)
                    .Select(e => e.Id)
                    .Take(500)
                    .ToListAsync(ct);

                var allIds = completedIds.Concat(failedIds).ToList();
                if (!allIds.Any()) return 0;

                await db.WorkflowExecutionLogs
                    .Where(l => allIds.Contains(l.ExecutionId))
                    .ExecuteDeleteAsync(ct);

                await db.Set<WorkflowApproval>()
                    .Where(a => allIds.Contains(a.ExecutionId))
                    .ExecuteDeleteAsync(ct);

                int deleted = await db.WorkflowExecutions
                    .Where(e => allIds.Contains(e.Id))
                    .ExecuteDeleteAsync(ct);

                if (deleted > 0)
                    _logger.LogInformation("[WORKFLOW-POLLING] 🗑️  TTL purge: removed {Count} execution records", deleted);

                return deleted;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WORKFLOW-POLLING] Stale execution purge failed (non-critical)");
                return 0;
            }
        }

        /// <summary>
        /// Find pending approval records whose ExpiresAt has passed, mark them as expired,
        /// and resume the workflow on the rejection/timeout branch so executions don't stall
        /// forever waiting for a human who never responds.
        /// </summary>
        private async Task<int> ExpireTimedOutApprovalsAsync(
            ApplicationDbContext db,
            IWorkflowGraphExecutor graphExecutor,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var timedOut = await db.Set<WorkflowApproval>()
                .Where(a => a.Status == "pending" && a.ExpiresAt != null && a.ExpiresAt <= now)
                .Take(50)
                .ToListAsync(ct);

            int expiredCount = 0;
            foreach (var approval in timedOut)
            {
                try
                {
                    _logger.LogInformation(
                        "[WORKFLOW-POLLING] ⏰ Approval #{ApprovalId} expired (execution #{ExecutionId}, node {NodeId}, deadline {Deadline:o})",
                        approval.Id, approval.ExecutionId, approval.NodeId, approval.ExpiresAt);

                    approval.Status = "expired";
                    approval.RespondedAt = now;
                    await db.SaveChangesAsync(ct);

                    var exec = await db.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == approval.ExecutionId, ct);
                    if (exec == null || exec.Status != "waiting_approval") continue;

                    Dictionary<string, object?> vars = new();
                    try
                    {
                        if (!string.IsNullOrEmpty(exec.Context))
                        {
                            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(exec.Context);
                            if (parsed != null)
                                foreach (var kv in parsed) vars[kv.Key] = kv.Value;
                        }
                    }
                    catch { /* best-effort */ }

                    // Downstream nodes can check approval_result == "expired" / "rejected"
                    vars["approval_result"] = "expired";
                    vars["approval_id"] = approval.Id;
                    vars["approval_expired"] = true;

                    var context = new WorkflowExecutionContext
                    {
                        WorkflowId = exec.WorkflowId,
                        ExecutionId = exec.Id,
                        TriggerEntityType = exec.TriggerEntityType,
                        TriggerEntityId = exec.TriggerEntityId,
                        UserId = "system-expiry",
                        Variables = vars
                    };

                    exec.Status = "running";
                    exec.WaitingNodeId = null;
                    await db.SaveChangesAsync(ct);

                    // Treat expiry as an implicit rejection — resume on the same branch
                    // the graph would take after a reject response.
                    var result = await graphExecutor.ResumeAfterNodeAsync(
                        exec.WorkflowId, exec.Id, approval.NodeId, context);

                    exec.Status = result.FinalStatus;
                    exec.Error = result.Success ? exec.Error : Truncate(result.Error, 1000);
                    if (result.FinalStatus == "completed" || result.FinalStatus == "failed")
                        exec.CompletedAt = now;
                    await db.SaveChangesAsync(ct);

                    expiredCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WORKFLOW-POLLING] Failed to expire approval #{ApprovalId}", approval.Id);
                    try
                    {
                        db.ChangeTracker.Clear();
                        var freshApproval = await db.Set<WorkflowApproval>().FirstOrDefaultAsync(a => a.Id == approval.Id, ct);
                        if (freshApproval != null && freshApproval.Status == "pending")
                        {
                            freshApproval.Status = "expired";
                            freshApproval.RespondedAt = now;
                            await db.SaveChangesAsync(ct);
                        }
                    }
                    catch { /* swallow */ }
                }
            }
            return expiredCount;
        }

        /// <summary>
        /// Find executions parked on a delay node whose ResumeAt is past, and resume the
        /// graph from successors of the waiting node.
        /// </summary>
        private async Task<int> ResumeDueDelayedExecutionsAsync(
            ApplicationDbContext db,
            IWorkflowGraphExecutor graphExecutor,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var due = await db.WorkflowExecutions
                .Where(e => e.Status == "waiting_delay"
                         && e.ResumeAt != null
                         && e.ResumeAt <= now
                         && e.WaitingNodeId != null)
                .Take(50)
                .ToListAsync(ct);

            int resumed = 0;
            foreach (var exec in due)
            {
                try
                {
                    _logger.LogInformation(
                        "[WORKFLOW-POLLING] ⏰ Resuming execution {Id} (delay node {NodeId} due since {ResumeAt:o})",
                        exec.Id, exec.WaitingNodeId, exec.ResumeAt);

                    var pausedNodeId = exec.WaitingNodeId!;
                    exec.Status = "running";
                    exec.ResumeAt = null;
                    exec.WaitingNodeId = null;
                    await db.SaveChangesAsync(ct);

                    Dictionary<string, object?> vars = new();
                    try
                    {
                        if (!string.IsNullOrEmpty(exec.Context))
                        {
                            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(exec.Context);
                            if (parsed != null)
                            {
                                foreach (var kv in parsed) vars[kv.Key] = kv.Value;
                            }
                        }
                    }
                    catch { /* best-effort */ }

                    var context = new WorkflowExecutionContext
                    {
                        WorkflowId = exec.WorkflowId,
                        ExecutionId = exec.Id,
                        TriggerEntityType = exec.TriggerEntityType,
                        TriggerEntityId = exec.TriggerEntityId,
                        UserId = exec.TriggeredBy,
                        Variables = vars
                    };

                    var result = await graphExecutor.ResumeAfterNodeAsync(
                        exec.WorkflowId, exec.Id, pausedNodeId, context);

                    exec.Status = result.FinalStatus;
                    exec.Error = Truncate(result.Error, 1000);
                    if (result.FinalStatus == "completed" || result.FinalStatus == "failed")
                    {
                        exec.CompletedAt = DateTime.UtcNow;
                    }
                    await db.SaveChangesAsync(ct);
                    resumed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WORKFLOW-POLLING] Failed to resume delayed execution {Id}", exec.Id);
                    try
                    {
                        db.ChangeTracker.Clear();
                        var fresh = await db.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == exec.Id, ct);
                        if (fresh != null)
                        {
                            fresh.Status = "failed";
                            fresh.Error = Truncate(ex.Message, 1000);
                            fresh.CompletedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(ct);
                        }
                    }
                    catch { /* swallow */ }
                }
            }
            return resumed;
        }

        /// <summary>
        /// Walk active workflow definitions for scheduled-trigger nodes and fire them when
        /// their intervalMinutes has elapsed since the last scheduled execution. The last-fire
        /// marker is the max(StartedAt) of WorkflowExecutions whose TriggeredBy starts with
        /// "scheduled:{nodeId}".
        /// </summary>
        private async Task<int> FireDueScheduledTriggersAsync(
            ApplicationDbContext db,
            IWorkflowGraphExecutor graphExecutor,
            CancellationToken ct)
        {
            int fired = 0;
            var now = DateTime.UtcNow;

            var workflows = await db.WorkflowDefinitions
                .Where(w => w.IsActive && !w.IsDeleted)
                .ToListAsync(ct);

            foreach (var workflow in workflows)
            {
                List<JsonElement>? nodes = null;
                try { nodes = JsonSerializer.Deserialize<List<JsonElement>>(workflow.Nodes); }
                catch { continue; }
                if (nodes == null) continue;

                foreach (var nodeEl in nodes)
                {
                    var nodeId = nodeEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    if (string.IsNullOrEmpty(nodeId)) continue;

                    // Determine business type
                    string nodeType = "";
                    if (nodeEl.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
                    {
                        if (dataEl.TryGetProperty("type", out var dtEl)) nodeType = dtEl.GetString() ?? "";
                    }
                    if (string.IsNullOrEmpty(nodeType) && nodeEl.TryGetProperty("type", out var tEl)) nodeType = tEl.GetString() ?? "";
                    if (!nodeType.Contains("scheduled", StringComparison.OrdinalIgnoreCase)) continue;

                    // Read interval (default 60 minutes)
                    int intervalMinutes = 60;
                    if (nodeEl.TryGetProperty("data", out var d2) && d2.ValueKind == JsonValueKind.Object)
                    {
                        if (d2.TryGetProperty("intervalMinutes", out var iEl) && iEl.ValueKind == JsonValueKind.Number)
                            intervalMinutes = iEl.GetInt32();
                        else if (d2.TryGetProperty("config", out var cEl) && cEl.ValueKind == JsonValueKind.Object
                                 && cEl.TryGetProperty("intervalMinutes", out var ciEl) && ciEl.ValueKind == JsonValueKind.Number)
                            intervalMinutes = ciEl.GetInt32();
                    }
                    if (intervalMinutes < 1) intervalMinutes = 1;

                    var marker = $"scheduled:{nodeId}";
                    var lastFired = await db.WorkflowExecutions
                        .Where(e => e.WorkflowId == workflow.Id && e.TriggeredBy == marker)
                        .OrderByDescending(e => e.StartedAt)
                        .Select(e => (DateTime?)e.StartedAt)
                        .FirstOrDefaultAsync(ct);

                    if (lastFired.HasValue && lastFired.Value.AddMinutes(intervalMinutes) > now) continue;

                    try
                    {
                        var exec = new WorkflowExecution
                        {
                            WorkflowId = workflow.Id,
                            TriggerEntityType = "scheduled",
                            TriggerEntityId = 0,
                            Status = "running",
                            CurrentNodeId = nodeId,
                            Context = JsonSerializer.Serialize(new { source = "scheduler", nodeId, intervalMinutes, firedAt = now }),
                            StartedAt = now,
                            TriggeredBy = marker
                        };
                        db.WorkflowExecutions.Add(exec);
                        await db.SaveChangesAsync(ct);

                        var context = new WorkflowExecutionContext
                        {
                            WorkflowId = workflow.Id,
                            ExecutionId = exec.Id,
                            TriggerEntityType = "scheduled",
                            TriggerEntityId = 0,
                            UserId = marker,
                            Variables = new Dictionary<string, object?>
                            {
                                ["triggerSource"] = "scheduler",
                                ["scheduledNodeId"] = nodeId,
                                ["intervalMinutes"] = intervalMinutes
                            }
                        };

                        var result = await graphExecutor.ExecuteGraphAsync(workflow.Id, exec.Id, nodeId!, context);

                        exec.Status = result.FinalStatus;
                        exec.Error = Truncate(result.Error, 1000);
                        if (result.FinalStatus == "completed" || result.FinalStatus == "failed")
                            exec.CompletedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(ct);

                        fired++;
                        _logger.LogInformation(
                            "[WORKFLOW-POLLING] ⏱️  Fired scheduled trigger '{NodeId}' in workflow #{WorkflowId} (interval={Interval}m, result={Status})",
                            nodeId, workflow.Id, intervalMinutes, result.FinalStatus);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[WORKFLOW-POLLING] Failed to fire scheduled trigger '{NodeId}' in workflow #{WorkflowId}", nodeId, workflow.Id);
                    }
                }
            }

            return fired;
        }

        /// <summary>
        /// Direct status consistency reconciliation.
        /// Checks for status mismatches between entities and fixes them directly,
        /// independent of the workflow trigger/processed tracking system.
        /// This is the safety net that catches anything the event-based triggers missed.
        /// </summary>
        /// <summary>
        /// WORKFLOW-DRIVEN status consistency reconciliation.
        /// Instead of hardcoded rules, reads ALL active workflow definitions,
        /// extracts the condition→action rules the user configured,
        /// and applies them dynamically. Supports infinite combinations of
        /// entity types, statuses, and cascading rules.
        /// </summary>
        private async Task<int> ReconcileStatusConsistencyAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            int totalFixes = 0;

            try
            {
                // ═══ PHASE 1A: WORKFLOW-DRIVEN STATUS RECONCILIATION ═══
                var activeWorkflows = await db.WorkflowDefinitions
                    .Where(w => w.IsActive && !w.IsDeleted)
                    .ToListAsync(cancellationToken);

                _logger.LogInformation(
                    "[WORKFLOW-RECONCILE] Analyzing {Count} active workflows for consistency rules",
                    activeWorkflows.Count);

                var allRules = new List<ReconciliationRule>();

                foreach (var workflow in activeWorkflows)
                {
                    var nodes = ParseReconcileNodes(workflow.Nodes);
                    var edges = ParseReconcileEdges(workflow.Edges);
                    var rules = ExtractReconciliationRules(nodes, edges);
                    allRules.AddRange(rules);

                    if (rules.Any())
                    {
                        _logger.LogInformation(
                            "[WORKFLOW-RECONCILE] Workflow '{Name}' (#{Id}): extracted {Count} reconciliation rules",
                            workflow.Name, workflow.Id, rules.Count);
                    }
                }

                // Apply direct cascade rules first, then collection rules
                // (collection rules are more specific and can override direct cascades)
                foreach (var rule in allRules.Where(r => r.IsDirect))
                {
                    try
                    {
                        totalFixes += await ApplyDirectCascadeRuleAsync(db, rule, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[WORKFLOW-RECONCILE] Error applying direct rule: {Desc}", rule.Description);
                    }
                }

                foreach (var rule in allRules.Where(r => r.IsCollectionRule))
                {
                    try
                    {
                        totalFixes += await ApplyCollectionRuleAsync(db, rule, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[WORKFLOW-RECONCILE] Error applying collection rule: {Desc}", rule.Description);
                    }
                }

                // ═══ PHASE 1B: DATA INTEGRITY FIXES (always run, non-workflow) ═══
                totalFixes += await FixMissingStartDatesAsync(db, cancellationToken);
                totalFixes += await FixCompletedDispatchCountsAsync(db, cancellationToken);
                totalFixes += await FixMissingOfferIdsAsync(db, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW-RECONCILE] Error during status consistency reconciliation");
            }

            return totalFixes;
        }

        #region Workflow Graph Parsing for Reconciliation

        private List<ReconcileNode> ParseReconcileNodes(string nodesJson)
        {
            try
            {
                var elements = JsonSerializer.Deserialize<List<JsonElement>>(nodesJson);
                if (elements == null) return new List<ReconcileNode>();
                return elements.Select(ParseReconcileNode).Where(n => n != null).Cast<ReconcileNode>().ToList();
            }
            catch { return new List<ReconcileNode>(); }
        }

        private ReconcileNode? ParseReconcileNode(JsonElement el)
        {
            try
            {
                var node = new ReconcileNode
                {
                    Id = el.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    Type = el.TryGetProperty("type", out var type) ? type.GetString() ?? "" : ""
                };

                if (el.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    node.Label = data.TryGetProperty("label", out var label) ? label.GetString() ?? "" : "";

                    // Business type from data.type overrides React Flow type
                    if (data.TryGetProperty("type", out var dataType))
                    {
                        var bt = dataType.GetString();
                        if (!string.IsNullOrEmpty(bt)) node.Type = bt;
                    }

                    foreach (var prop in data.EnumerateObject())
                    {
                        node.Data[prop.Name] = prop.Value.Clone();
                    }
                }

                return node;
            }
            catch { return null; }
        }

        private List<ReconcileEdge> ParseReconcileEdges(string edgesJson)
        {
            try
            {
                var elements = JsonSerializer.Deserialize<List<JsonElement>>(edgesJson);
                if (elements == null) return new List<ReconcileEdge>();
                return elements.Select(el => new ReconcileEdge
                {
                    Id = el.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    Source = el.TryGetProperty("source", out var src) ? src.GetString() ?? "" : "",
                    Target = el.TryGetProperty("target", out var tgt) ? tgt.GetString() ?? "" : "",
                    SourceHandle = el.TryGetProperty("sourceHandle", out var sh) ? sh.GetString() : null,
                    Label = el.TryGetProperty("label", out var lbl) ? lbl.GetString() : null
                }).ToList();
            }
            catch { return new List<ReconcileEdge>(); }
        }

        #endregion

        #region Rule Extraction from Workflow Graph

        /// <summary>
        /// Extracts reconciliation rules from a workflow graph.
        /// Supports two patterns:
        /// 1. Condition → Action (branching): e.g., "all dispatches completed? → YES: SO=tech_completed / NO: SO=partial"
        /// 2. Trigger → Direct Action (no condition): e.g., "dispatch in_progress → SO in_progress"
        /// </summary>
        private List<ReconciliationRule> ExtractReconciliationRules(List<ReconcileNode> nodes, List<ReconcileEdge> edges)
        {
            var rules = new List<ReconciliationRule>();
            var adjacency = edges.GroupBy(e => e.Source).ToDictionary(g => g.Key, g => g.ToList());

            // ─── Pattern 1: Condition → Action chains ───
            foreach (var condNode in nodes.Where(n => IsConditionNodeType(n.Type)))
            {
                if (!adjacency.TryGetValue(condNode.Id, out var outEdges)) continue;

                var field = condNode.GetDataString("field")
                         ?? condNode.GetConfigString("field")
                         ?? condNode.GetConditionDataString("field");
                var op = condNode.GetDataString("operator")
                      ?? condNode.GetConfigString("operator")
                      ?? condNode.GetConditionDataString("operator");
                var value = condNode.GetDataString("value")
                         ?? condNode.GetConfigString("value")
                         ?? condNode.GetConditionDataString("value");
                var checkField = condNode.GetConditionDataString("checkField")
                              ?? condNode.GetConfigString("checkField");

                // Only process collection-based conditions (e.g., serviceOrder.dispatches)
                if (string.IsNullOrEmpty(field) || !field.Contains('.')) continue;

                var yesBranch = outEdges.FirstOrDefault(e => IsYesBranch(e));
                var noBranch = outEdges.FirstOrDefault(e => IsNoBranch(e));

                var yesAction = yesBranch != null ? nodes.FirstOrDefault(n => n.Id == yesBranch.Target) : null;
                var noAction = noBranch != null ? nodes.FirstOrDefault(n => n.Id == noBranch.Target) : null;

                string? yesEntityType = null, yesStatus = null, noEntityType = null, noStatus = null;

                if (yesAction != null && IsUpdateStatusNodeType(yesAction.Type))
                {
                    yesEntityType = yesAction.GetDataString("entityType") ?? InferEntityTypeFromNodeType(yesAction.Type);
                    yesStatus = yesAction.GetDataString("newStatus") ?? yesAction.GetConfigString("newStatus");
                }
                if (noAction != null && IsUpdateStatusNodeType(noAction.Type))
                {
                    noEntityType = noAction.GetDataString("entityType") ?? InferEntityTypeFromNodeType(noAction.Type);
                    noStatus = noAction.GetDataString("newStatus") ?? noAction.GetConfigString("newStatus");
                }

                if (yesStatus == null && noStatus == null) continue;

                rules.Add(new ReconciliationRule
                {
                    IsCollectionRule = true,
                    ConditionField = field,
                    ConditionOperator = op ?? "all_match",
                    ConditionValue = value,
                    ConditionCheckField = checkField ?? "status",
                    YesTargetEntityType = yesEntityType,
                    YesTargetStatus = yesStatus,
                    NoTargetEntityType = noEntityType,
                    NoTargetStatus = noStatus,
                    Description = $"Collection: {field} {op} [{value}] → YES:{yesEntityType}={yesStatus}, NO:{noEntityType}={noStatus}"
                });
            }

            // ─── Pattern 2: Trigger → Direct Action (no condition in between) ───
            foreach (var triggerNode in nodes.Where(n => n.Type.Contains("status-trigger")))
            {
                if (!adjacency.TryGetValue(triggerNode.Id, out var outEdges)) continue;

                foreach (var edge in outEdges)
                {
                    var targetNode = nodes.FirstOrDefault(n => n.Id == edge.Target);
                    if (targetNode == null) continue;
                    if (IsConditionNodeType(targetNode.Type)) continue; // Skip if next is a condition
                    if (!IsUpdateStatusNodeType(targetNode.Type)) continue;

                    var triggerEntityType = InferEntityTypeFromNodeType(triggerNode.Type);
                    var triggerToStatus = triggerNode.GetDataString("toStatus");
                    var actionEntityType = targetNode.GetDataString("entityType") ?? InferEntityTypeFromNodeType(targetNode.Type);
                    var actionNewStatus = targetNode.GetDataString("newStatus") ?? targetNode.GetConfigString("newStatus");
                    var condition = targetNode.GetConfigString("condition");

                    if (triggerEntityType == null || actionEntityType == null || actionNewStatus == null) continue;
                    if (triggerEntityType == actionEntityType) continue; // Skip self-updates

                    rules.Add(new ReconciliationRule
                    {
                        IsDirect = true,
                        DirectTriggerEntityType = triggerEntityType,
                        DirectTriggerStatus = triggerToStatus,
                        DirectCondition = condition,
                        YesTargetEntityType = actionEntityType,
                        YesTargetStatus = actionNewStatus,
                        Description = $"Direct: {triggerEntityType}={triggerToStatus} → {actionEntityType}={actionNewStatus}" +
                                      (condition != null ? $" (condition: {condition})" : "")
                    });
                }
            }

            return rules;
        }

        #endregion

        #region Rule Application

        /// <summary>
        /// Apply a collection-based rule (condition → YES/NO action).
        /// Example: "if all serviceOrder.dispatches all_match [technically_completed,completed] → SO=technically_completed; else SO=partially_completed"
        /// </summary>
        private async Task<int> ApplyCollectionRuleAsync(ApplicationDbContext db, ReconciliationRule rule, CancellationToken ct)
        {
            int fixes = 0;

            var parts = rule.ConditionField!.Split('.', 2);
            var parentPrefix = parts[0].ToLower().Replace("_", "");
            var collectionName = parts[1].ToLower();

            var parentEntityType = ResolveEntityType(parentPrefix);
            if (parentEntityType == null) return 0;

            // ── serviceOrder.dispatches ──
            if (parentEntityType == "service_order" && collectionName == "dispatches")
            {
                var serviceOrders = await db.ServiceOrders
                    .Where(so => so.Status != "closed" && so.Status != "cancelled")
                    .Select(so => new
                    {
                        so.Id,
                        so.Status,
                        Dispatches = db.Dispatches
                            .Where(d => d.ServiceOrderId == so.Id && !d.IsDeleted)
                            .Select(d => new { d.Id, d.Status })
                            .ToList()
                    })
                    .ToListAsync(ct);

                foreach (var so in serviceOrders)
                {
                    if (!so.Dispatches.Any()) continue;

                    var childStatuses = so.Dispatches.Select(d => d.Status ?? "").ToList();
                    var conditionMet = EvaluateCollectionCondition(
                        childStatuses,
                        rule.ConditionOperator ?? "all_match",
                        rule.ConditionValue ?? "");

                    string? expectedStatus = conditionMet ? rule.YesTargetStatus : rule.NoTargetStatus;
                    string? expectedEntityType = conditionMet ? rule.YesTargetEntityType : rule.NoTargetEntityType;

                    if (expectedStatus == null) continue;
                    if (expectedEntityType != null && expectedEntityType != "service_order") continue;
                    if (so.Status == expectedStatus) continue;

                    var serviceOrder = await db.ServiceOrders.FindAsync(so.Id);
                    if (serviceOrder == null) continue;

                    var oldStatus = serviceOrder.Status;
                    serviceOrder.Status = expectedStatus;
                    serviceOrder.ModifiedDate = DateTime.UtcNow;
                    serviceOrder.ModifiedBy = "system-reconcile";

                    // Keep CompletedDispatchCount on the one canonical definition shared by
                    // ServiceOrderStatusCalculator (live, non-deleted dispatches whose status is
                    // completed/technically_completed). It used to be derived from whatever status
                    // list the matched workflow rule happened to carry, which made the counter
                    // drift away from every other writer.
                    serviceOrder.CompletedDispatchCount = so.Dispatches
                        .Count(d => MyApi.Modules.ServiceOrders.Services.ServiceOrderStatusCalculator.IsCompletedDispatchStatus(d.Status));


                    // Set timestamps based on new status
                    if (expectedStatus == "in_progress" && !serviceOrder.ActualStartDate.HasValue)
                        serviceOrder.ActualStartDate = DateTime.UtcNow;
                    if (expectedStatus == "technically_completed" || expectedStatus == "completed")
                    {
                        serviceOrder.TechnicallyCompletedAt ??= DateTime.UtcNow;
                        serviceOrder.ActualCompletionDate ??= DateTime.UtcNow;
                    }

                    await db.SaveChangesAsync(ct);
                    fixes++;

                    _logger.LogInformation(
                        "[WORKFLOW-RECONCILE] 🔧 Collection rule: SO #{Id} '{Old}' → '{New}' (condition={Met}, dispatches: {Matched}/{Total})",
                        so.Id, oldStatus, expectedStatus, conditionMet ? "YES" : "NO",
                        so.Dispatches.Count(d => MyApi.Modules.ServiceOrders.Services.ServiceOrderStatusCalculator.IsCompletedDispatchStatus(d.Status)), so.Dispatches.Count);
                }
            }
            // ── sale.serviceOrders or sale.service_orders ──
            else if (parentEntityType == "sale" && (collectionName == "serviceorders" || collectionName == "service_orders"))
            {
                var sales = await db.Sales
                    .Where(s => s.Status != "closed" && s.Status != "cancelled")
                    .Select(s => new
                    {
                        s.Id,
                        s.Status,
                        ServiceOrders = db.ServiceOrders
                            .Where(so => so.SaleId == s.Id.ToString())
                            .Select(so => new { so.Id, so.Status })
                            .ToList()
                    })
                    .ToListAsync(ct);

                foreach (var sale in sales)
                {
                    if (!sale.ServiceOrders.Any()) continue;

                    var childStatuses = sale.ServiceOrders.Select(so => so.Status ?? "").ToList();
                    var conditionMet = EvaluateCollectionCondition(
                        childStatuses,
                        rule.ConditionOperator ?? "all_match",
                        rule.ConditionValue ?? "");

                    string? expectedStatus = conditionMet ? rule.YesTargetStatus : rule.NoTargetStatus;
                    string? expectedEntityType = conditionMet ? rule.YesTargetEntityType : rule.NoTargetEntityType;

                    if (expectedStatus == null) continue;
                    if (expectedEntityType != null && expectedEntityType != "sale") continue;
                    if (sale.Status == expectedStatus) continue;

                    var saleEntity = await db.Sales.FindAsync(sale.Id);
                    if (saleEntity == null) continue;

                    var oldStatus = saleEntity.Status;
                    saleEntity.Status = expectedStatus;
                    saleEntity.ModifiedDate = DateTime.UtcNow;
                    saleEntity.ModifiedBy = "system-reconcile";

                    await db.SaveChangesAsync(ct);
                    fixes++;

                    _logger.LogInformation(
                        "[WORKFLOW-RECONCILE] 🔧 Collection rule: Sale #{Id} '{Old}' → '{New}' (condition={Met})",
                        sale.Id, oldStatus, expectedStatus, conditionMet ? "YES" : "NO");
                }
            }
            // ── sale.items (typically for creation, skip for status reconciliation) ──
            else if (parentEntityType == "sale" && collectionName == "items")
            {
                _logger.LogDebug("[WORKFLOW-RECONCILE] Skipping sale.items collection rule (creation logic, not status)");
            }
            // ── offer.sales ──
            else if (parentEntityType == "offer" && collectionName == "sales")
            {
                var offers = await db.Offers
                    .Where(o => o.Status != "cancelled" && o.Status != "rejected" && o.Status != "expired")
                    .Select(o => new
                    {
                        o.Id,
                        o.Status,
                        Sales = db.Sales
                            .Where(s => s.OfferId == o.Id.ToString())
                            .Select(s => new { s.Id, s.Status })
                            .ToList()
                    })
                    .ToListAsync(ct);

                foreach (var offer in offers)
                {
                    if (!offer.Sales.Any()) continue;

                    var childStatuses = offer.Sales.Select(s => s.Status ?? "").ToList();
                    var conditionMet = EvaluateCollectionCondition(
                        childStatuses,
                        rule.ConditionOperator ?? "all_match",
                        rule.ConditionValue ?? "");

                    string? expectedStatus = conditionMet ? rule.YesTargetStatus : rule.NoTargetStatus;
                    string? expectedEntityType = conditionMet ? rule.YesTargetEntityType : rule.NoTargetEntityType;

                    if (expectedStatus == null) continue;
                    if (expectedEntityType != null && expectedEntityType != "offer") continue;
                    if (offer.Status == expectedStatus) continue;

                    var offerEntity = await db.Offers.FindAsync(offer.Id);
                    if (offerEntity == null) continue;

                    var oldStatus = offerEntity.Status;
                    offerEntity.Status = expectedStatus;
                    offerEntity.ModifiedDate = DateTime.UtcNow;
                    offerEntity.ModifiedBy = "system-reconcile";

                    await db.SaveChangesAsync(ct);
                    fixes++;

                    _logger.LogInformation(
                        "[WORKFLOW-RECONCILE] 🔧 Collection rule: Offer #{Id} '{Old}' → '{New}' (condition={Met})",
                        offer.Id, oldStatus, expectedStatus, conditionMet ? "YES" : "NO");
                }
            }
            // ── serviceOrder.jobs ──
            else if (parentEntityType == "service_order" && collectionName == "jobs")
            {
                var serviceOrders = await db.ServiceOrders
                    .Where(so => so.Status != "closed" && so.Status != "cancelled")
                    .Select(so => new
                    {
                        so.Id,
                        so.Status,
                        Jobs = db.ServiceOrderJobs
                            .Where(j => j.ServiceOrderId == so.Id)
                            .Select(j => new { j.Id, j.Status })
                            .ToList()
                    })
                    .ToListAsync(ct);

                foreach (var so in serviceOrders)
                {
                    if (!so.Jobs.Any()) continue;

                    var childStatuses = so.Jobs.Select(j => j.Status ?? "").ToList();
                    var conditionMet = EvaluateCollectionCondition(
                        childStatuses,
                        rule.ConditionOperator ?? "all_match",
                        rule.ConditionValue ?? "");

                    string? expectedStatus = conditionMet ? rule.YesTargetStatus : rule.NoTargetStatus;
                    string? expectedEntityType = conditionMet ? rule.YesTargetEntityType : rule.NoTargetEntityType;

                    if (expectedStatus == null) continue;
                    if (expectedEntityType != null && expectedEntityType != "service_order") continue;
                    if (so.Status == expectedStatus) continue;

                    var serviceOrder = await db.ServiceOrders.FindAsync(so.Id);
                    if (serviceOrder == null) continue;

                    var oldStatus = serviceOrder.Status;
                    serviceOrder.Status = expectedStatus;
                    serviceOrder.ModifiedDate = DateTime.UtcNow;
                    serviceOrder.ModifiedBy = "system-reconcile";

                    if (expectedStatus == "in_progress" && !serviceOrder.ActualStartDate.HasValue)
                        serviceOrder.ActualStartDate = DateTime.UtcNow;
                    if (expectedStatus.Contains("completed") || expectedStatus.Contains("finished"))
                    {
                        serviceOrder.TechnicallyCompletedAt ??= DateTime.UtcNow;
                        serviceOrder.ActualCompletionDate ??= DateTime.UtcNow;
                    }

                    await db.SaveChangesAsync(ct);
                    fixes++;

                    _logger.LogInformation(
                        "[WORKFLOW-RECONCILE] 🔧 Collection rule: SO #{Id} '{Old}' → '{New}' (jobs condition={Met})",
                        so.Id, oldStatus, expectedStatus, conditionMet ? "YES" : "NO");
                }
            }

            return fixes;
        }

        /// <summary>
        /// Apply a direct cascade rule (trigger status → parent status update).
        /// Example: "dispatch in_progress → SO in_progress (ifNotAlreadyInProgress)"
        /// Uses status ordering to prevent downgrades via direct cascade.
        /// </summary>
        private async Task<int> ApplyDirectCascadeRuleAsync(ApplicationDbContext db, ReconciliationRule rule, CancellationToken ct)
        {
            int fixes = 0;

            if (rule.DirectTriggerEntityType == null || rule.YesTargetEntityType == null || rule.YesTargetStatus == null)
                return 0;

            // ── dispatch → service_order ──
            if (rule.DirectTriggerEntityType == "dispatch" && rule.YesTargetEntityType == "service_order")
            {
                var dispatchesByServiceOrder = await db.Dispatches
                    .Where(d => !d.IsDeleted && d.Status == rule.DirectTriggerStatus && d.ServiceOrderId != null)
                    .Select(d => d.ServiceOrderId!.Value)
                    .Distinct()
                    .ToListAsync(ct);

                foreach (var soId in dispatchesByServiceOrder)
                {
                    var so = await db.ServiceOrders.FindAsync(soId);
                    if (so == null || so.Status == "closed" || so.Status == "cancelled") continue;

                    // Check direct condition (e.g., ifNotAlreadyInProgress)
                    if (rule.DirectCondition == "ifNotAlreadyInProgress" && so.Status == rule.YesTargetStatus) continue;

                    // Only apply if SO is in a lower status (prevent downgrades via direct cascade)
                    var currentOrder = GetStatusOrder("service_order", so.Status ?? "");
                    var targetOrder = GetStatusOrder("service_order", rule.YesTargetStatus);
                    if (currentOrder >= targetOrder) continue;

                    var oldStatus = so.Status;
                    so.Status = rule.YesTargetStatus;
                    so.ModifiedDate = DateTime.UtcNow;
                    so.ModifiedBy = "system-reconcile";

                    if (rule.YesTargetStatus == "in_progress" && !so.ActualStartDate.HasValue)
                        so.ActualStartDate = DateTime.UtcNow;

                    await db.SaveChangesAsync(ct);
                    fixes++;

                    _logger.LogInformation(
                        "[WORKFLOW-RECONCILE] 🔧 Direct cascade: SO #{Id} '{Old}' → '{New}' (triggered by dispatch status '{TriggerStatus}')",
                        soId, oldStatus, rule.YesTargetStatus, rule.DirectTriggerStatus);
                }
            }
            // ── service_order → sale ──
            else if (rule.DirectTriggerEntityType == "service_order" && rule.YesTargetEntityType == "sale")
            {
                var serviceOrders = await db.ServiceOrders
                    .Where(so => so.Status == rule.DirectTriggerStatus && so.SaleId != null)
                    .Select(so => new { so.Id, so.SaleId })
                    .ToListAsync(ct);

                foreach (var soInfo in serviceOrders)
                {
                    if (!int.TryParse(soInfo.SaleId, out var saleId)) continue;
                    var sale = await db.Sales.FindAsync(saleId);
                    if (sale == null || sale.Status == "closed" || sale.Status == "cancelled") continue;

                    var currentOrder = GetStatusOrder("sale", sale.Status ?? "");
                    var targetOrder = GetStatusOrder("sale", rule.YesTargetStatus);
                    if (currentOrder >= targetOrder) continue;

                    var oldStatus = sale.Status;
                    sale.Status = rule.YesTargetStatus;
                    sale.ModifiedDate = DateTime.UtcNow;
                    sale.ModifiedBy = "system-reconcile";

                    await db.SaveChangesAsync(ct);
                    fixes++;

                    _logger.LogInformation(
                        "[WORKFLOW-RECONCILE] 🔧 Direct cascade: Sale #{Id} '{Old}' → '{New}' (triggered by SO status '{TriggerStatus}')",
                        saleId, oldStatus, rule.YesTargetStatus, rule.DirectTriggerStatus);
                }
            }
            // ── sale → offer ──
            else if (rule.DirectTriggerEntityType == "sale" && rule.YesTargetEntityType == "offer")
            {
                var sales = await db.Sales
                    .Where(s => s.Status == rule.DirectTriggerStatus && s.OfferId != null)
                    .Select(s => new { s.Id, s.OfferId })
                    .ToListAsync(ct);

                foreach (var saleInfo in sales)
                {
                    if (!int.TryParse(saleInfo.OfferId, out var offerId)) continue;
                    var offer = await db.Offers.FindAsync(offerId);
                    if (offer == null || offer.Status == "cancelled" || offer.Status == "rejected") continue;

                    var currentOrder = GetStatusOrder("offer", offer.Status ?? "");
                    var targetOrder = GetStatusOrder("offer", rule.YesTargetStatus);
                    if (currentOrder >= targetOrder) continue;

                    var oldStatus = offer.Status;
                    offer.Status = rule.YesTargetStatus;
                    offer.ModifiedDate = DateTime.UtcNow;
                    offer.ModifiedBy = "system-reconcile";

                    await db.SaveChangesAsync(ct);
                    fixes++;

                    _logger.LogInformation(
                        "[WORKFLOW-RECONCILE] 🔧 Direct cascade: Offer #{Id} '{Old}' → '{New}' (triggered by Sale status '{TriggerStatus}')",
                        offerId, oldStatus, rule.YesTargetStatus, rule.DirectTriggerStatus);
                }
            }
            // ── dispatch → sale (through service order) ──
            else if (rule.DirectTriggerEntityType == "dispatch" && rule.YesTargetEntityType == "sale")
            {
                var dispatches = await db.Dispatches
                    .Where(d => !d.IsDeleted && d.Status == rule.DirectTriggerStatus && d.ServiceOrderId != null)
                    .Select(d => d.ServiceOrderId!.Value)
                    .Distinct()
                    .ToListAsync(ct);

                foreach (var soId in dispatches)
                {
                    var so = await db.ServiceOrders.AsNoTracking().FirstOrDefaultAsync(s => s.Id == soId, ct);
                    if (so?.SaleId == null || !int.TryParse(so.SaleId, out var saleId)) continue;

                    var sale = await db.Sales.FindAsync(saleId);
                    if (sale == null || sale.Status == "closed" || sale.Status == "cancelled") continue;

                    var currentOrder = GetStatusOrder("sale", sale.Status ?? "");
                    var targetOrder = GetStatusOrder("sale", rule.YesTargetStatus);
                    if (currentOrder >= targetOrder) continue;

                    var oldStatus = sale.Status;
                    sale.Status = rule.YesTargetStatus;
                    sale.ModifiedDate = DateTime.UtcNow;
                    sale.ModifiedBy = "system-reconcile";

                    await db.SaveChangesAsync(ct);
                    fixes++;

                    _logger.LogInformation(
                        "[WORKFLOW-RECONCILE] 🔧 Direct cascade: Sale #{Id} '{Old}' → '{New}' (via SO#{SoId}, dispatch status '{TriggerStatus}')",
                        saleId, oldStatus, rule.YesTargetStatus, soId, rule.DirectTriggerStatus);
                }
            }

            return fixes;
        }

        #endregion

        #region Condition Evaluation Helpers

        /// <summary>
        /// Evaluates a collection condition (all_match, any_match, contains, none_match).
        /// Works with any entity's child statuses.
        /// </summary>
        private bool EvaluateCollectionCondition(List<string> childStatuses, string operatorType, string expectedValue)
        {
            var expectedValues = expectedValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim().ToLower()).ToHashSet();

            if (!expectedValues.Any()) return false;

            return operatorType.ToLower() switch
            {
                "all_match" => childStatuses.All(s => expectedValues.Contains(s?.ToLower() ?? "")),
                "any_match" => childStatuses.Any(s => expectedValues.Contains(s?.ToLower() ?? "")),
                "contains" => childStatuses.Any(s => expectedValues.Contains(s?.ToLower() ?? "")),
                "none_match" => childStatuses.All(s => !expectedValues.Contains(s?.ToLower() ?? "")),
                _ => false
            };
        }

        /// <summary>
        /// Status ordering per entity type. Used by direct cascade rules
        /// to prevent downgrades (only upgrade status, never go backwards).
        /// Collection rules CAN downgrade because they have explicit YES/NO branches.
        /// </summary>
        private int GetStatusOrder(string entityType, string status)
        {
            return entityType switch
            {
                "service_order" => status switch
                {
                    "draft" => 0, "pending" => 1, "planned" => 3, "ready_for_planning" => 2, "scheduled" => 3,
                    "in_progress" => 4, "on_hold" => 4, "partially_completed" => 5,
                    "technically_completed" => 6, "ready_for_invoice" => 7, "completed" => 8, "invoiced" => 9, "closed" => 10,
                    "cancelled" => -1, _ => 0
                },
                "sale" => status switch
                {
                    "created" => 0, "in_progress" => 1, "partially_invoiced" => 2,
                    "invoiced" => 3, "closed" => 4, "cancelled" => -1, _ => 0
                },
                "dispatch" => status switch
                {
                    // Flow: assigned -> confirmed -> in_progress -> completed
                    // (pending/planned kept as legacy pre-assigned states)
                    "pending" => 0, "planned" => 0, "assigned" => 1, "acknowledged" => 2,
                    "confirmed" => 2, "en_route" => 3, "on_site" => 3, "in_progress" => 4,
                    "technically_completed" => 5, "completed" => 6,
                    "cancelled" => -1, "rejected" => -1, _ => 0
                },
                "offer" => status switch
                {
                    "draft" => 0, "sent" => 1, "pending" => 2, "negotiation" => 3,
                    "accepted" => 4, "won" => 5, "lost" => -1, "cancelled" => -1,
                    "rejected" => -1, "expired" => -1, "declined" => -1, "modified" => 3, _ => 0
                },
                // Deals use "stage" as their pipeline state (lead → … → won/lost).
                "deal" => status switch
                {
                    "lead" => 0, "new" => 0, "qualified" => 1, "proposal" => 2,
                    "negotiation" => 3, "won" => 4, "closed_won" => 4,
                    "lost" => -1, "closed_lost" => -1, _ => 0
                },
                _ => 0
            };
        }

        #endregion

        #region Node Type Detection Helpers

        private bool IsConditionNodeType(string type)
        {
            var t = type.ToLower();
            return t.Contains("condition") || t.Contains("if-") || t.Contains("if_else");
        }

        private bool IsUpdateStatusNodeType(string type)
        {
            var t = type.ToLower();
            return t.Contains("update-") && t.Contains("status");
        }

        private bool IsYesBranch(ReconcileEdge edge)
        {
            var handle = edge.SourceHandle?.ToLower() ?? "";
            var label = edge.Label?.ToLower() ?? "";
            return handle == "yes" || handle == "true" || label == "yes" || label == "true";
        }

        private bool IsNoBranch(ReconcileEdge edge)
        {
            var handle = edge.SourceHandle?.ToLower() ?? "";
            var label = edge.Label?.ToLower() ?? "";
            return handle == "no" || handle == "false" || label == "no" || label == "false";
        }

        private string? InferEntityTypeFromNodeType(string nodeType)
        {
            var t = nodeType.ToLower();
            if (t.Contains("service-order") || t.Contains("service_order")) return "service_order";
            if (t.Contains("dispatch")) return "dispatch";
            if (t.Contains("sale")) return "sale";
            if (t.Contains("offer")) return "offer";
            return null;
        }

        private string? ResolveEntityType(string prefix)
        {
            if (prefix.Contains("serviceorder") || prefix.Contains("service_order")) return "service_order";
            if (prefix.Contains("sale")) return "sale";
            if (prefix.Contains("offer")) return "offer";
            if (prefix.Contains("dispatch")) return "dispatch";
            return null;
        }

        #endregion

        #region Data Integrity Fixes

        private async Task<int> FixMissingStartDatesAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var sosMissing = await db.ServiceOrders
                .Where(so => so.Status == "in_progress" && so.ActualStartDate == null)
                .ToListAsync(ct);

            foreach (var so in sosMissing)
            {
                so.ActualStartDate = DateTime.UtcNow;
                so.ModifiedDate = DateTime.UtcNow;
                so.ModifiedBy = "system-reconcile";
                _logger.LogInformation("[WORKFLOW-RECONCILE] 🔧 Set missing ActualStartDate for SO #{Id}", so.Id);
            }

            if (sosMissing.Any()) await db.SaveChangesAsync(ct);
            return sosMissing.Count;
        }

        private async Task<int> FixCompletedDispatchCountsAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var mismatches = await db.ServiceOrders
                .Where(so => so.Status != "draft" && so.Status != "cancelled" && so.Status != "closed")
                .Select(so => new
                {
                    so.Id,
                    so.CompletedDispatchCount,
                    Actual = db.Dispatches
                        .Count(d => d.ServiceOrderId == so.Id && !d.IsDeleted &&
                            (d.Status == "technically_completed" || d.Status == "completed"))
                })
                .Where(x => x.CompletedDispatchCount != x.Actual)
                .ToListAsync(ct);

            foreach (var m in mismatches)
            {
                var so = await db.ServiceOrders.FindAsync(m.Id);
                if (so != null)
                {
                    so.CompletedDispatchCount = m.Actual;
                    so.ModifiedDate = DateTime.UtcNow;
                    so.ModifiedBy = "system-reconcile";
                    _logger.LogInformation("[WORKFLOW-RECONCILE] 🔧 CompletedDispatchCount SO #{Id}: {Old}→{New}", m.Id, m.CompletedDispatchCount, m.Actual);
                }
            }

            if (mismatches.Any()) await db.SaveChangesAsync(ct);
            return mismatches.Count;
        }

        private async Task<int> FixMissingOfferIdsAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var sosMissing = await db.ServiceOrders
                .Where(so => so.OfferId == null && so.SaleId != null)
                .ToListAsync(ct);

            int fixes = 0;
            foreach (var so in sosMissing)
            {
                if (int.TryParse(so.SaleId, out var saleId))
                {
                    var sale = await db.Sales.FindAsync(saleId);
                    if (sale?.OfferId != null)
                    {
                        so.OfferId = sale.OfferId;
                        so.ModifiedDate = DateTime.UtcNow;
                        so.ModifiedBy = "system-reconcile";
                        fixes++;
                        _logger.LogInformation("[WORKFLOW-RECONCILE] 🔧 Set OfferId={OId} for SO #{Id} from Sale #{SId}", sale.OfferId, so.Id, saleId);
                    }
                }
            }

            if (sosMissing.Any()) await db.SaveChangesAsync(ct);
            return fixes;
        }

        #endregion

        #region Helper Classes

        private class ReconcileNode
        {
            public string Id { get; set; } = "";
            public string Type { get; set; } = "";
            public string Label { get; set; } = "";
            public Dictionary<string, JsonElement> Data { get; set; } = new();

            public string? GetDataString(string key)
            {
                if (Data.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
                    return el.GetString();
                return null;
            }

            public string? GetConfigString(string key)
            {
                if (Data.TryGetValue("config", out var config) && config.ValueKind == JsonValueKind.Object)
                {
                    if (config.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                        return prop.GetString();
                }
                return null;
            }

            public string? GetConditionDataString(string key)
            {
                // Check config.conditionData
                if (Data.TryGetValue("config", out var config) && config.ValueKind == JsonValueKind.Object)
                {
                    if (config.TryGetProperty("conditionData", out var condData) && condData.ValueKind == JsonValueKind.Object)
                    {
                        if (condData.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                            return prop.GetString();
                    }
                }
                // Also check top-level conditionData
                if (Data.TryGetValue("conditionData", out var topCondData) && topCondData.ValueKind == JsonValueKind.Object)
                {
                    if (topCondData.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                        return prop.GetString();
                }
                return null;
            }
        }

        private class ReconcileEdge
        {
            public string Id { get; set; } = "";
            public string Source { get; set; } = "";
            public string Target { get; set; } = "";
            public string? SourceHandle { get; set; }
            public string? Label { get; set; }
        }

        private class ReconciliationRule
        {
            public bool IsCollectionRule { get; set; }
            public bool IsDirect { get; set; }

            // For collection rules (condition-based)
            public string? ConditionField { get; set; }       // e.g., "serviceOrder.dispatches"
            public string? ConditionOperator { get; set; }    // e.g., "all_match"
            public string? ConditionValue { get; set; }       // e.g., "technically_completed,completed"
            public string? ConditionCheckField { get; set; }  // e.g., "status"
            public string? YesTargetEntityType { get; set; }
            public string? YesTargetStatus { get; set; }
            public string? NoTargetEntityType { get; set; }
            public string? NoTargetStatus { get; set; }

            // For direct cascade rules
            public string? DirectTriggerEntityType { get; set; }  // e.g., "dispatch"
            public string? DirectTriggerStatus { get; set; }       // e.g., "in_progress"
            public string? DirectCondition { get; set; }           // e.g., "ifNotAlreadyInProgress"

            public string Description { get; set; } = "";
        }

        #endregion

        private async Task<(int processed, int triggered)> ProcessTriggerAsync(
            ApplicationDbContext db,
            WorkflowTrigger trigger,
            IWorkflowGraphExecutor graphExecutor,
            IWorkflowNotificationService notificationService,
            CancellationToken cancellationToken)
        {
            int processed = 0;
            int triggered = 0;

            _logger.LogInformation(
                "[WORKFLOW-POLLING] Processing trigger {TriggerId}: {EntityType} -> '{ToStatus}' (from: '{FromStatus}')",
                trigger.Id, trigger.EntityType, trigger.ToStatus ?? "ANY", trigger.FromStatus ?? "ANY");

            // Get entities that match the trigger's toStatus (current status check)
            var matchingEntities = await GetEntitiesWithStatusAsync(db, trigger.EntityType, trigger.ToStatus, cancellationToken);
            
            _logger.LogInformation(
                "[WORKFLOW-POLLING] Found {Count} {EntityType} entities with status '{Status}'",
                matchingEntities.Count, trigger.EntityType, trigger.ToStatus ?? "ANY");

            foreach (var entity in matchingEntities)
            {
                processed++;

                // Check if this entity has already been processed by this trigger for this status
                var alreadyProcessed = await db.Set<WorkflowProcessedEntity>()
                    .AnyAsync(p => p.TriggerId == trigger.Id 
                        && p.EntityType == trigger.EntityType 
                        && p.EntityId == entity.Id 
                        && p.ProcessedStatus == entity.Status, 
                        cancellationToken);

                if (alreadyProcessed)
                {
                    _logger.LogDebug(
                        "[WORKFLOW-POLLING] Skipping {EntityType} #{EntityId} - already processed for status '{Status}'",
                        trigger.EntityType, entity.Id, entity.Status);
                    continue;
                }

                _logger.LogInformation(
                    "[WORKFLOW-POLLING] 🚀 Triggering workflow for {EntityType} #{EntityId} (Status: '{Status}')",
                    trigger.EntityType, entity.Id, entity.Status);

                WorkflowExecution? execution = null;
                int? executionId = null;

                try
                {
                    // Create workflow execution
                    execution = await CreateExecutionAsync(db, trigger, entity, notificationService);
                    executionId = execution.Id;

                    // Mark as processed BEFORE executing to prevent race conditions.
                    // The AnyAsync check above is a TOCTOU read: two concurrent polling
                    // passes (or a poll racing a webhook) can both see "not processed".
                    // The unique index on (TenantId, TriggerId, EntityType, EntityId,
                    // ProcessedStatus) is the real guard — if the insert conflicts,
                    // another worker already claimed this entity, so skip it.
                    var processedRecord = new WorkflowProcessedEntity
                    {
                        TriggerId = trigger.Id,
                        EntityType = trigger.EntityType,
                        EntityId = entity.Id,
                        ProcessedStatus = entity.Status,
                        ProcessedAt = DateTime.UtcNow,
                        ExecutionId = execution.Id
                    };
                    db.Set<WorkflowProcessedEntity>().Add(processedRecord);

                    try
                    {
                        await db.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateException dupEx) when (IsUniqueViolation(dupEx))
                    {
                        db.Entry(processedRecord).State = EntityState.Detached;
                        _logger.LogInformation(
                            "[WORKFLOW-POLLING] {EntityType} #{EntityId} was claimed concurrently for status '{Status}' — skipping duplicate execution",
                            trigger.EntityType, entity.Id, entity.Status);

                        // Roll the just-created execution back so it doesn't linger as "running".
                        var orphan = await db.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == execution.Id, cancellationToken);
                        if (orphan != null)
                        {
                            db.WorkflowExecutions.Remove(orphan);
                            await db.SaveChangesAsync(cancellationToken);
                        }
                        continue;
                    }


                    // Execute the workflow graph
                    var context = new WorkflowExecutionContext
                    {
                        WorkflowId = trigger.WorkflowId,
                        ExecutionId = execution.Id,
                        TriggerEntityType = trigger.EntityType,
                        TriggerEntityId = entity.Id,
                        UserId = "system-polling",
                        Variables = new Dictionary<string, object?>
                        {
                            ["oldStatus"] = entity.Status, // For state-based, old = current (we don't know the real old)
                            ["newStatus"] = entity.Status,
                            ["entityId"] = entity.Id,
                            ["entityType"] = trigger.EntityType,
                            ["triggerSource"] = "polling",
                            ["additionalContext"] = entity.Context
                        }
                    };

                    // Pre-populate related entity IDs in context for faster lookups
                    await PopulateRelatedEntityIdsAsync(db, trigger.EntityType, entity.Id, entity.Context, context.Variables);

                    var result = await graphExecutor.ExecuteGraphAsync(
                        trigger.WorkflowId,
                        execution.Id,
                        trigger.NodeId,
                        context);

                    // Update execution with result
                    execution.Status = result.FinalStatus;
                    execution.Error = Truncate(result.Error, 1000);
                    if (result.FinalStatus == "completed" || result.FinalStatus == "failed")
                    {
                        execution.CompletedAt = DateTime.UtcNow;
                    }

                    // IMPORTANT: workflow nodes may have attempted entity updates that failed (schema mismatch, etc.)
                    // If the DbContext is in a bad state, saving the execution update can throw.
                    try
                    {
                        await db.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx,
                            "[WORKFLOW-POLLING] Failed to persist execution status for execution #{ExecutionId}. Retrying with a clean ChangeTracker.",
                            execution.Id);

                        var execId = execution.Id;
                        var finalStatus = execution.Status;
                        var finalError = execution.Error;
                        var completedAt = execution.CompletedAt;

                        db.ChangeTracker.Clear();

                        var freshExecution = await db.WorkflowExecutions
                            .FirstOrDefaultAsync(e => e.Id == execId, cancellationToken);

                        if (freshExecution != null)
                        {
                            freshExecution.Status = finalStatus;
                            freshExecution.Error = finalError;
                            if (finalStatus == "completed" || finalStatus == "failed")
                            {
                                freshExecution.CompletedAt = completedAt ?? DateTime.UtcNow;
                            }

                            await db.SaveChangesAsync(cancellationToken);
                        }
                    }

                    await notificationService.NotifyExecutionCompletedAsync(
                        trigger.WorkflowId,
                        execution.Id,
                        result.FinalStatus,
                        result.NodesExecuted,
                        result.NodesFailed);

                    _logger.LogInformation(
                        "[WORKFLOW-POLLING] ✅ Workflow completed for {EntityType} #{EntityId}: {Status} ({Executed} nodes executed)",
                        trigger.EntityType, entity.Id, result.FinalStatus, result.NodesExecuted);

                    triggered++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[WORKFLOW-POLLING] ❌ Failed to execute workflow for {EntityType} #{EntityId}",
                        trigger.EntityType, entity.Id);

                    // Best-effort cleanup:
                    // 1) mark execution failed (so debug console doesn't show perpetual 'running')
                    // 2) remove processed marker so polling can retry after config/schema fixes
                    try
                    {
                        var now = DateTime.UtcNow;

                        db.ChangeTracker.Clear();

                        // Remove processed marker(s) so this status can be retried later
                        var markers = await db.Set<WorkflowProcessedEntity>()
                            .Where(p => p.TriggerId == trigger.Id
                                && p.EntityType == trigger.EntityType
                                && p.EntityId == entity.Id
                                && p.ProcessedStatus == entity.Status)
                            .ToListAsync(cancellationToken);

                        if (markers.Any())
                        {
                            db.Set<WorkflowProcessedEntity>().RemoveRange(markers);
                        }

                        if (executionId.HasValue)
                        {
                            var exec = await db.WorkflowExecutions
                                .FirstOrDefaultAsync(e => e.Id == executionId.Value, cancellationToken);

                            if (exec != null)
                            {
                                exec.Status = "failed";
                                exec.Error = Truncate(ex.Message, 1000);
                                exec.CompletedAt = now;
                            }
                        }

                        await db.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogWarning(innerEx,
                            "[WORKFLOW-POLLING] Failed to persist failure/cleanup for {EntityType} #{EntityId}",
                            trigger.EntityType, entity.Id);
                    }
                }
            }

            return (processed, triggered);
        }

        private async Task<List<EntityStatusInfo>> GetEntitiesWithStatusAsync(
            ApplicationDbContext db,
            string entityType,
            string? targetStatus,
            CancellationToken cancellationToken)
        {
            var results = new List<EntityStatusInfo>();

            switch (entityType.ToLower())
            {
                case "dispatch":
                    var dispatches = await db.Dispatches
                        .AsNoTracking()
                        .Where(d => !d.IsDeleted && (targetStatus == null || d.Status == targetStatus))
                        .Select(d => new { d.Id, d.Status, d.ServiceOrderId, d.JobId })
                        .ToListAsync(cancellationToken);
                    
                    results.AddRange(dispatches.Select(d => new EntityStatusInfo
                    {
                        Id = d.Id,
                        Status = d.Status ?? "",
                        Context = new { d.ServiceOrderId, d.JobId }
                    }));
                    break;

                case "service_order":
                    var serviceOrders = await db.ServiceOrders
                        .AsNoTracking()
                        .Where(so => targetStatus == null || so.Status == targetStatus)
                        .Select(so => new { so.Id, so.Status, so.SaleId })
                        .ToListAsync(cancellationToken);
                    
                    results.AddRange(serviceOrders.Select(so => new EntityStatusInfo
                    {
                        Id = so.Id,
                        Status = so.Status ?? "",
                        Context = new { so.SaleId }
                    }));
                    break;

                case "sale":
                    var sales = await db.Sales
                        .AsNoTracking()
                        .Where(s => targetStatus == null || s.Status == targetStatus)
                        .Select(s => new { s.Id, s.Status, s.OfferId })
                        .ToListAsync(cancellationToken);
                    
                    results.AddRange(sales.Select(s => new EntityStatusInfo
                    {
                        Id = s.Id,
                        Status = s.Status ?? "",
                        Context = new { s.OfferId }
                    }));
                    break;

                case "offer":
                    var offers = await db.Offers
                        .AsNoTracking()
                        .Where(o => targetStatus == null || o.Status == targetStatus)
                        .Select(o => new { o.Id, o.Status })
                        .ToListAsync(cancellationToken);

                    results.AddRange(offers.Select(o => new EntityStatusInfo
                    {
                        Id = o.Id,
                        Status = o.Status ?? "",
                        Context = null
                    }));
                    break;

                case "deal":
                    // Deals key their pipeline state on "Stage", not "Status".
                    var deals = await db.Deals
                        .AsNoTracking()
                        .Where(d => !d.IsDeleted && (targetStatus == null || d.Stage == targetStatus))
                        .Select(d => new { d.Id, d.Stage, d.ContactId, d.ProjectId })
                        .ToListAsync(cancellationToken);

                    results.AddRange(deals.Select(d => new EntityStatusInfo
                    {
                        Id = d.Id,
                        Status = d.Stage ?? "",
                        Context = new { d.ContactId, d.ProjectId }
                    }));
                    break;
            }

            return results;
        }

        private async Task<WorkflowExecution> CreateExecutionAsync(
            ApplicationDbContext db,
            WorkflowTrigger trigger,
            EntityStatusInfo entity,
            IWorkflowNotificationService notificationService)
        {
            var execution = new WorkflowExecution
            {
                WorkflowId = trigger.WorkflowId,
                TriggerEntityType = trigger.EntityType,
                TriggerEntityId = entity.Id,
                Status = "running",
                CurrentNodeId = trigger.NodeId,
                Context = JsonSerializer.Serialize(new
                {
                    entityType = trigger.EntityType,
                    entityId = entity.Id,
                    currentStatus = entity.Status,
                    triggerSource = "polling",
                    triggeredAt = DateTime.UtcNow,
                    additionalContext = entity.Context
                }),
                StartedAt = DateTime.UtcNow,
                TriggeredBy = "system-polling"
            };

            db.WorkflowExecutions.Add(execution);
            await db.SaveChangesAsync();

            await notificationService.NotifyExecutionStartedAsync(
                trigger.WorkflowId,
                execution.Id,
                trigger.EntityType,
                entity.Id,
                "system-polling");

            // Log trigger node as completed
            var triggerLog = new WorkflowExecutionLog
            {
                ExecutionId = execution.Id,
                NodeId = trigger.NodeId,
                NodeType = "status-trigger",
                Status = "completed",
                Input = JsonSerializer.Serialize(new { currentStatus = entity.Status, source = "polling" }),
                Output = JsonSerializer.Serialize(new { triggered = true }),
                Timestamp = DateTime.UtcNow
            };
            db.WorkflowExecutionLogs.Add(triggerLog);
            await db.SaveChangesAsync();

            await notificationService.NotifyNodeCompletedAsync(
                trigger.WorkflowId,
                execution.Id,
                trigger.NodeId,
                "status-trigger",
                true,
                null,
                JsonSerializer.Serialize(new { triggered = true, source = "polling" }));

            return execution;
        }
        
        /// <summary>
        /// Pre-populate related entity IDs in the context for faster lookups during workflow execution.
        /// This resolves the full entity chain upfront.
        /// </summary>
        private async Task PopulateRelatedEntityIdsAsync(
            ApplicationDbContext db,
            string entityType,
            int entityId,
            object? entityContext,
            Dictionary<string, object?> variables)
        {
            try
            {
                switch (entityType.ToLower())
                {
                    case "dispatch":
                        // Dispatch → ServiceOrder → Sale → Offer
                        var dispatch = await db.Dispatches.FindAsync(entityId);
                        if (dispatch?.ServiceOrderId != null)
                        {
                            variables["serviceOrderId"] = dispatch.ServiceOrderId.Value;
                            _logger.LogInformation("[WORKFLOW-POLLING] Pre-populated serviceOrderId={Id}", dispatch.ServiceOrderId.Value);
                            
                            var so = await db.ServiceOrders.FindAsync(dispatch.ServiceOrderId.Value);
                            if (so?.SaleId != null && int.TryParse(so.SaleId, out var saleId))
                            {
                                variables["saleId"] = saleId;
                                _logger.LogInformation("[WORKFLOW-POLLING] Pre-populated saleId={Id}", saleId);
                                
                                var sale = await db.Sales.FindAsync(saleId);
                                if (sale?.OfferId != null && int.TryParse(sale.OfferId, out var offerId))
                                {
                                    variables["offerId"] = offerId;
                                    _logger.LogInformation("[WORKFLOW-POLLING] Pre-populated offerId={Id}", offerId);
                                }
                            }
                        }
                        break;
                        
                    case "service_order":
                        // ServiceOrder → Sale → Offer
                        var serviceOrder = await db.ServiceOrders.FindAsync(entityId);
                        if (serviceOrder?.SaleId != null && int.TryParse(serviceOrder.SaleId, out var soSaleId))
                        {
                            variables["saleId"] = soSaleId;
                            _logger.LogInformation("[WORKFLOW-POLLING] Pre-populated saleId={Id}", soSaleId);
                            
                            var soSale = await db.Sales.FindAsync(soSaleId);
                            if (soSale?.OfferId != null && int.TryParse(soSale.OfferId, out var soOfferId))
                            {
                                variables["offerId"] = soOfferId;
                                _logger.LogInformation("[WORKFLOW-POLLING] Pre-populated offerId={Id}", soOfferId);
                            }
                        }
                        break;
                        
                    case "sale":
                        // Sale → Offer
                        var sale2 = await db.Sales.FindAsync(entityId);
                        if (sale2?.OfferId != null && int.TryParse(sale2.OfferId, out var saleOfferId))
                        {
                            variables["offerId"] = saleOfferId;
                            _logger.LogInformation("[WORKFLOW-POLLING] Pre-populated offerId={Id}", saleOfferId);
                        }
                        // Sale → ServiceOrder (if exists)
                        var relatedSo = await db.ServiceOrders.FirstOrDefaultAsync(s => s.SaleId == entityId.ToString());
                        if (relatedSo != null)
                        {
                            variables["serviceOrderId"] = relatedSo.Id;
                            _logger.LogInformation("[WORKFLOW-POLLING] Pre-populated serviceOrderId={Id}", relatedSo.Id);
                        }
                        break;
                        
                    case "offer":
                        // Offer → Sale → ServiceOrder
                        var relatedSale = await db.Sales.FirstOrDefaultAsync(s => s.OfferId == entityId.ToString());
                        if (relatedSale != null)
                        {
                            variables["saleId"] = relatedSale.Id;
                            _logger.LogInformation("[WORKFLOW-POLLING] Pre-populated saleId={Id}", relatedSale.Id);

                            var offerSo = await db.ServiceOrders.FirstOrDefaultAsync(s => s.SaleId == relatedSale.Id.ToString());
                            if (offerSo != null)
                            {
                                variables["serviceOrderId"] = offerSo.Id;
                                _logger.LogInformation("[WORKFLOW-POLLING] Pre-populated serviceOrderId={Id}", offerSo.Id);
                            }
                        }
                        break;

                    case "deal":
                        // Deal → Contact / Project / converted Offer / Sale
                        var deal = await db.Deals.FindAsync(entityId);
                        if (deal != null)
                        {
                            variables["contactId"] = deal.ContactId;
                            if (deal.ProjectId != null) variables["projectId"] = deal.ProjectId.Value;
                            if (!string.IsNullOrEmpty(deal.ConvertedToOfferId) && int.TryParse(deal.ConvertedToOfferId, out var dOfferId))
                                variables["offerId"] = dOfferId;
                            if (!string.IsNullOrEmpty(deal.ConvertedToSaleId) && int.TryParse(deal.ConvertedToSaleId, out var dSaleId))
                                variables["saleId"] = dSaleId;
                            _logger.LogInformation("[WORKFLOW-POLLING] Pre-populated deal context for deal #{Id}", entityId);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WORKFLOW-POLLING] Failed to pre-populate related entity IDs for {EntityType}#{EntityId}",
                    entityType, entityId);
            }
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        /// <summary>
        /// True when a save failed because of a unique-constraint violation
        /// (Postgres SQLSTATE 23505 / SQL Server 2601-2627). Used to turn the
        /// dedupe index into the authoritative "already claimed" signal.
        /// </summary>
        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            var message = (ex.InnerException?.Message ?? ex.Message);
            return message.Contains("23505", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Internal class to hold entity status information
        /// </summary>
        private class EntityStatusInfo
        {
            public int Id { get; set; }
            public string Status { get; set; } = "";
            public object? Context { get; set; }
        }
    }
}

