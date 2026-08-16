using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.WorkflowEngine.Models;
using MyApi.Modules.WorkflowEngine.Services;
using System.Text.Json;

namespace MyApi.Modules.WorkflowEngine.Controllers
{
    /// <summary>
    /// Manual “run now” endpoint for the state-based workflow polling cycle.
    ///
    /// The frontend workflow builder calls this to reconcile entity states immediately
    /// (instead of waiting for the 5-minute background polling interval).
    /// </summary>
    [ApiController]
    [Route("api/workflow-reconciliation")]
    [Authorize]
    public class WorkflowReconciliationController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowReconciliationController> _logger;

        // Must match the UI expectations (src/services/api/workflowApi.ts)
        public class ReconciliationResultDto
        {
            public int OffersFixed { get; set; }
            public int SalesFixed { get; set; }
            public int ServiceOrdersFixed { get; set; }
            public int DispatchesFixed { get; set; }
            public int JobsFixed { get; set; }
            public int ConsistencyFixes { get; set; }
            public int TotalFixed { get; set; }
            public string Message { get; set; } = "";
        }

        public class ReconciliationStatusDto
        {
            public bool Enabled { get; set; }
            public int IntervalMinutes { get; set; }
            public string Description { get; set; } = "";
        }

        public WorkflowReconciliationController(
            IServiceProvider serviceProvider,
            ILogger<WorkflowReconciliationController> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Runs one full polling cycle immediately.
        /// This is effectively “poll now”.
        /// </summary>
        [HttpPost("run")]
        public async Task<ActionResult<ReconciliationResultDto>> Run(CancellationToken cancellationToken)
        {
            var startedAt = DateTime.UtcNow;

            _logger.LogInformation("[WORKFLOW-RECONCILE] Manual run requested at {Time}", startedAt);

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<IWorkflowNotificationService>();
            var graphExecutor = scope.ServiceProvider.GetRequiredService<IWorkflowGraphExecutor>();

            // Phase 1: Direct status consistency reconciliation
            var consistencyFixes = await ReconcileStatusConsistencyAsync(db, cancellationToken);
            _logger.LogInformation("[WORKFLOW-RECONCILE] Phase 1 - Consistency fixes: {Fixes}", consistencyFixes);

            // Get all active triggers with their workflows (same criteria as WorkflowPollingService)
            var triggers = await db.WorkflowTriggers
                .Include(t => t.Workflow)
                .Where(t => t.IsActive
                    && t.Workflow != null
                    && t.Workflow.IsActive
                    && !t.Workflow.IsDeleted)
                .ToListAsync(cancellationToken);

            int totalProcessed = 0;
            int totalTriggered = 0;

            int offersFixed = 0;
            int salesFixed = 0;
            int serviceOrdersFixed = 0;
            int dispatchesFixed = 0;
            int jobsFixed = 0;

            foreach (var trigger in triggers)
            {
                try
                {
                    var (processed, triggered) = await ProcessTriggerAsync(
                        db,
                        trigger,
                        graphExecutor,
                        notificationService,
                        cancellationToken);

                    totalProcessed += processed;
                    totalTriggered += triggered;

                    // “Fixed” counts are a best-effort metric for UI only:
                    // count how many executions were triggered by entity type.
                    switch (trigger.EntityType?.ToLower())
                    {
                        case "offer":
                            offersFixed += triggered;
                            break;
                        case "sale":
                            salesFixed += triggered;
                            break;
                        case "service_order":
                            serviceOrdersFixed += triggered;
                            break;
                        case "dispatch":
                            dispatchesFixed += triggered;
                            break;
                        case "job":
                            jobsFixed += triggered;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[WORKFLOW-RECONCILE] Error processing trigger {TriggerId} (Node: {NodeId})",
                        trigger.Id,
                        trigger.NodeId);
                }
            }

            var endedAt = DateTime.UtcNow;

            var result = new ReconciliationResultDto
            {
                OffersFixed = offersFixed,
                SalesFixed = salesFixed,
                ServiceOrdersFixed = serviceOrdersFixed,
                DispatchesFixed = dispatchesFixed,
                JobsFixed = jobsFixed,
                ConsistencyFixes = consistencyFixes,
                TotalFixed = totalTriggered + consistencyFixes,
                Message = $"Reconciliation complete in {(endedAt - startedAt).TotalSeconds:F1}s. Consistency fixes: {consistencyFixes}. Entities checked: {totalProcessed}. Workflows triggered: {totalTriggered}."
            };

            _logger.LogInformation("[WORKFLOW-RECONCILE] {Message}", result.Message);

            return Ok(result);
        }

        /// <summary>
        /// Exposes the background polling configuration so the UI can display it.
        /// </summary>
        [HttpGet("status")]
        public ActionResult<ReconciliationStatusDto> Status()
        {
            // Keep in sync with WorkflowPollingService (TimeSpan.FromMinutes(5))
            return Ok(new ReconciliationStatusDto
            {
                Enabled = true,
                IntervalMinutes = 5,
                Description = "State-based workflow polling: checks current entity status and triggers workflows (same as background service)."
            });
        }

        private async Task<(int processed, int triggered)> ProcessTriggerAsync(
            ApplicationDbContext db,
            WorkflowTrigger trigger,
            IWorkflowGraphExecutor graphExecutor,
            IWorkflowNotificationService notificationService,
            CancellationToken cancellationToken)
        {
            int processed = 0;
            int triggered = 0;

            var matchingEntities = await GetEntitiesWithStatusAsync(db, trigger.EntityType, trigger.ToStatus, cancellationToken);

            foreach (var entity in matchingEntities)
            {
                processed++;

                var alreadyProcessed = await db.Set<WorkflowProcessedEntity>()
                    .AnyAsync(p => p.TriggerId == trigger.Id
                            && p.EntityType == trigger.EntityType
                            && p.EntityId == entity.Id
                            && p.ProcessedStatus == entity.Status,
                        cancellationToken);

                if (alreadyProcessed)
                {
                    continue;
                }

                WorkflowExecution? execution = null;
                int? executionId = null;

                try
                {
                    execution = await CreateExecutionAsync(db, trigger, entity, notificationService, cancellationToken);
                    executionId = execution.Id;

                    // Mark as processed BEFORE executing to prevent race conditions
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
                    await db.SaveChangesAsync(cancellationToken);

                    var context = new WorkflowExecutionContext
                    {
                        WorkflowId = trigger.WorkflowId,
                        ExecutionId = execution.Id,
                        TriggerEntityType = trigger.EntityType,
                        TriggerEntityId = entity.Id,
                        UserId = "manual-reconcile",
                        Variables = new Dictionary<string, object?>
                        {
                            ["oldStatus"] = entity.Status,
                            ["newStatus"] = entity.Status,
                            ["entityId"] = entity.Id,
                            ["entityType"] = trigger.EntityType,
                            ["triggerSource"] = "manual-reconcile",
                            ["additionalContext"] = entity.Context
                        }
                    };

                    await PopulateRelatedEntityIdsAsync(db, trigger.EntityType, entity.Id, entity.Context, context.Variables, cancellationToken);

                    var execResult = await graphExecutor.ExecuteGraphAsync(
                        trigger.WorkflowId,
                        execution.Id,
                        trigger.NodeId,
                        context);

                    execution.Status = execResult.FinalStatus;
                    execution.Error = Truncate(execResult.Error, 1000);
                    if (execResult.FinalStatus == "completed" || execResult.FinalStatus == "failed")
                    {
                        execution.CompletedAt = DateTime.UtcNow;
                    }

                    // Persist execution result (defensive: schema mismatches / failed entity updates can poison ChangeTracker)
                    try
                    {
                        await db.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx,
                            "[WORKFLOW-RECONCILE] Failed to persist execution status for execution #{ExecutionId}. Retrying with a clean ChangeTracker.",
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
                        execResult.FinalStatus,
                        execResult.NodesExecuted,
                        execResult.NodesFailed);

                    triggered++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[WORKFLOW-RECONCILE] Failed to execute workflow for {EntityType} #{EntityId}",
                        trigger.EntityType,
                        entity.Id);

                    // Best-effort cleanup:
                    // 1) mark execution failed
                    // 2) remove processed marker so the entity can be retried after config/schema fixes
                    try
                    {
                        var now = DateTime.UtcNow;

                        db.ChangeTracker.Clear();

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
                            "[WORKFLOW-RECONCILE] Failed to persist failure/cleanup for {EntityType} #{EntityId}",
                            trigger.EntityType,
                            entity.Id);
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

                case "job":
                    var jobs = await db.ServiceOrderJobs
                        .AsNoTracking()
                        .Where(j => targetStatus == null || j.Status == targetStatus)
                        .Select(j => new { j.Id, j.Status, j.ServiceOrderId })
                        .ToListAsync(cancellationToken);

                    results.AddRange(jobs.Select(j => new EntityStatusInfo
                    {
                        Id = j.Id,
                        Status = j.Status ?? "",
                        Context = new { j.ServiceOrderId }
                    }));
                    break;
            }

            return results;
        }

        private async Task<WorkflowExecution> CreateExecutionAsync(
            ApplicationDbContext db,
            WorkflowTrigger trigger,
            EntityStatusInfo entity,
            IWorkflowNotificationService notificationService,
            CancellationToken cancellationToken)
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
                    triggerSource = "manual-reconcile",
                    triggeredAt = DateTime.UtcNow,
                    additionalContext = entity.Context
                }),
                StartedAt = DateTime.UtcNow,
                TriggeredBy = User?.Identity?.Name ?? "manual-reconcile"
            };

            db.WorkflowExecutions.Add(execution);
            await db.SaveChangesAsync(cancellationToken);

            await notificationService.NotifyExecutionStartedAsync(
                trigger.WorkflowId,
                execution.Id,
                trigger.EntityType,
                entity.Id,
                "manual-reconcile");

            // Log trigger node as completed
            var triggerLog = new WorkflowExecutionLog
            {
                ExecutionId = execution.Id,
                NodeId = trigger.NodeId,
                NodeType = "status-trigger",
                Status = "completed",
                Input = JsonSerializer.Serialize(new { currentStatus = entity.Status, source = "manual-reconcile" }),
                Output = JsonSerializer.Serialize(new { triggered = true }),
                Timestamp = DateTime.UtcNow
            };

            db.WorkflowExecutionLogs.Add(triggerLog);
            await db.SaveChangesAsync(cancellationToken);

            await notificationService.NotifyNodeCompletedAsync(
                trigger.WorkflowId,
                execution.Id,
                trigger.NodeId,
                "status-trigger",
                true,
                null,
                JsonSerializer.Serialize(new { triggered = true, source = "manual-reconcile" }));

            return execution;
        }

        private async Task PopulateRelatedEntityIdsAsync(
            ApplicationDbContext db,
            string entityType,
            int entityId,
            object? entityContext,
            Dictionary<string, object?> variables,
            CancellationToken cancellationToken)
        {
            try
            {
                switch (entityType.ToLower())
                {
                    case "dispatch":
                        // Dispatch → ServiceOrder → Sale → Offer
                        var dispatch = await db.Dispatches.FindAsync(new object[] { entityId }, cancellationToken);
                        if (dispatch?.ServiceOrderId != null)
                        {
                            variables["serviceOrderId"] = dispatch.ServiceOrderId.Value;

                            var so = await db.ServiceOrders.FindAsync(new object[] { dispatch.ServiceOrderId.Value }, cancellationToken);
                            if (so?.SaleId != null && int.TryParse(so.SaleId, out var saleId))
                            {
                                variables["saleId"] = saleId;

                                var sale = await db.Sales.FindAsync(new object[] { saleId }, cancellationToken);
                                if (sale?.OfferId != null && int.TryParse(sale.OfferId, out var offerId))
                                {
                                    variables["offerId"] = offerId;
                                }
                            }
                        }
                        break;

                    case "service_order":
                        // ServiceOrder → Sale → Offer
                        var serviceOrder = await db.ServiceOrders.FindAsync(new object[] { entityId }, cancellationToken);
                        if (serviceOrder?.SaleId != null && int.TryParse(serviceOrder.SaleId, out var soSaleId))
                        {
                            variables["saleId"] = soSaleId;

                            var soSale = await db.Sales.FindAsync(new object[] { soSaleId }, cancellationToken);
                            if (soSale?.OfferId != null && int.TryParse(soSale.OfferId, out var soOfferId))
                            {
                                variables["offerId"] = soOfferId;
                            }
                        }
                        break;

                    case "sale":
                        // Sale → Offer + ServiceOrder
                        var sale2 = await db.Sales.FindAsync(new object[] { entityId }, cancellationToken);
                        if (sale2?.OfferId != null && int.TryParse(sale2.OfferId, out var saleOfferId))
                        {
                            variables["offerId"] = saleOfferId;
                        }

                        var relatedSo = await db.ServiceOrders
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.SaleId == entityId.ToString(), cancellationToken);

                        if (relatedSo != null)
                        {
                            variables["serviceOrderId"] = relatedSo.Id;
                        }
                        break;

                    case "offer":
                        // Offer → Sale → ServiceOrder
                        var relatedSale = await db.Sales
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.OfferId == entityId.ToString(), cancellationToken);

                        if (relatedSale != null)
                        {
                            variables["saleId"] = relatedSale.Id;

                            var offerSo = await db.ServiceOrders
                                .AsNoTracking()
                                .FirstOrDefaultAsync(s => s.SaleId == relatedSale.Id.ToString(), cancellationToken);

                            if (offerSo != null)
                            {
                                variables["serviceOrderId"] = offerSo.Id;
                            }
                        }
                        break;

                    case "job":
                        // Job → ServiceOrder → Sale → Offer
                        var jobEntity = await db.ServiceOrderJobs.FindAsync(new object[] { entityId }, cancellationToken);
                        if (jobEntity != null)
                        {
                            variables["serviceOrderId"] = jobEntity.ServiceOrderId;
                            var jobSo = await db.ServiceOrders.FindAsync(new object[] { jobEntity.ServiceOrderId }, cancellationToken);
                            if (jobSo?.SaleId != null && int.TryParse(jobSo.SaleId, out var jobSaleId))
                            {
                                variables["saleId"] = jobSaleId;
                                var jobSale = await db.Sales.FindAsync(new object[] { jobSaleId }, cancellationToken);
                                if (jobSale?.OfferId != null && int.TryParse(jobSale.OfferId, out var jobOfferId))
                                {
                                    variables["offerId"] = jobOfferId;
                                }
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[WORKFLOW-RECONCILE] Failed to pre-populate related entity IDs for {EntityType}#{EntityId}",
                    entityType,
                    entityId);
            }
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        /// <summary>
        /// WORKFLOW-DRIVEN status consistency reconciliation.
        /// Reads ALL active workflow definitions, extracts the condition→action rules
        /// the user configured, and applies them dynamically.
        /// Supports infinite combinations of entity types, statuses, and cascading rules.
        /// </summary>
        private async Task<int> ReconcileStatusConsistencyAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            int totalFixes = 0;

            try
            {
                // ═══ WORKFLOW-DRIVEN STATUS RECONCILIATION ═══
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
                            "[WORKFLOW-RECONCILE] Workflow '{Name}' (#{Id}): extracted {Count} rules",
                            workflow.Name, workflow.Id, rules.Count);
                    }
                }

                // Apply direct cascade rules first, then collection rules (collection can override)
                foreach (var rule in allRules.Where(r => r.IsDirect))
                {
                    try { totalFixes += await ApplyDirectCascadeRuleAsync(db, rule, cancellationToken); }
                    catch (Exception ex) { _logger.LogError(ex, "[WORKFLOW-RECONCILE] Error applying direct rule: {Desc}", rule.Description); }
                }
                foreach (var rule in allRules.Where(r => r.IsCollectionRule))
                {
                    try { totalFixes += await ApplyCollectionRuleAsync(db, rule, cancellationToken); }
                    catch (Exception ex) { _logger.LogError(ex, "[WORKFLOW-RECONCILE] Error applying collection rule: {Desc}", rule.Description); }
                }

                // ═══ DATA INTEGRITY FIXES ═══
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
                    if (data.TryGetProperty("type", out var dataType))
                    {
                        var bt = dataType.GetString();
                        if (!string.IsNullOrEmpty(bt)) node.Type = bt;
                    }
                    foreach (var prop in data.EnumerateObject())
                        node.Data[prop.Name] = prop.Value.Clone();
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

        #region Rule Extraction

        private List<ReconciliationRule> ExtractReconciliationRules(List<ReconcileNode> nodes, List<ReconcileEdge> edges)
        {
            var rules = new List<ReconciliationRule>();
            var adjacency = edges.GroupBy(e => e.Source).ToDictionary(g => g.Key, g => g.ToList());

            // Pattern 1: Condition → Action chains
            foreach (var condNode in nodes.Where(n => IsConditionNodeType(n.Type)))
            {
                if (!adjacency.TryGetValue(condNode.Id, out var outEdges)) continue;

                var field = condNode.GetDataString("field") ?? condNode.GetConfigString("field") ?? condNode.GetConditionDataString("field");
                var op = condNode.GetDataString("operator") ?? condNode.GetConfigString("operator") ?? condNode.GetConditionDataString("operator");
                var value = condNode.GetDataString("value") ?? condNode.GetConfigString("value") ?? condNode.GetConditionDataString("value");
                var checkField = condNode.GetConditionDataString("checkField") ?? condNode.GetConfigString("checkField");

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
                    ConditionField = field, ConditionOperator = op ?? "all_match",
                    ConditionValue = value, ConditionCheckField = checkField ?? "status",
                    YesTargetEntityType = yesEntityType, YesTargetStatus = yesStatus,
                    NoTargetEntityType = noEntityType, NoTargetStatus = noStatus,
                    Description = $"Collection: {field} {op} [{value}] → YES:{yesEntityType}={yesStatus}, NO:{noEntityType}={noStatus}"
                });
            }

            // Pattern 2: Trigger → Direct Action
            foreach (var triggerNode in nodes.Where(n => n.Type.Contains("status-trigger")))
            {
                if (!adjacency.TryGetValue(triggerNode.Id, out var outEdges)) continue;
                foreach (var edge in outEdges)
                {
                    var targetNode = nodes.FirstOrDefault(n => n.Id == edge.Target);
                    if (targetNode == null || IsConditionNodeType(targetNode.Type) || !IsUpdateStatusNodeType(targetNode.Type)) continue;

                    var triggerEntityType = InferEntityTypeFromNodeType(triggerNode.Type);
                    var triggerToStatus = triggerNode.GetDataString("toStatus");
                    var actionEntityType = targetNode.GetDataString("entityType") ?? InferEntityTypeFromNodeType(targetNode.Type);
                    var actionNewStatus = targetNode.GetDataString("newStatus") ?? targetNode.GetConfigString("newStatus");
                    var condition = targetNode.GetConfigString("condition");

                    if (triggerEntityType == null || actionEntityType == null || actionNewStatus == null) continue;
                    if (triggerEntityType == actionEntityType) continue;

                    rules.Add(new ReconciliationRule
                    {
                        IsDirect = true,
                        DirectTriggerEntityType = triggerEntityType, DirectTriggerStatus = triggerToStatus,
                        DirectCondition = condition,
                        YesTargetEntityType = actionEntityType, YesTargetStatus = actionNewStatus,
                        Description = $"Direct: {triggerEntityType}={triggerToStatus} → {actionEntityType}={actionNewStatus}"
                    });
                }
            }

            return rules;
        }

        #endregion

        #region Rule Application

        private async Task<int> ApplyCollectionRuleAsync(ApplicationDbContext db, ReconciliationRule rule, CancellationToken ct)
        {
            int fixes = 0;
            var parts = rule.ConditionField!.Split('.', 2);
            var parentPrefix = parts[0].ToLower().Replace("_", "");
            var collectionName = parts[1].ToLower();
            var parentEntityType = ResolveEntityType(parentPrefix);
            if (parentEntityType == null) return 0;

            if (parentEntityType == "service_order" && collectionName == "dispatches")
            {
                var serviceOrders = await db.ServiceOrders
                    .Where(so => so.Status != "closed" && so.Status != "cancelled")
                    .Select(so => new { so.Id, so.Status, Dispatches = db.Dispatches.Where(d => d.ServiceOrderId == so.Id && !d.IsDeleted).Select(d => new { d.Id, d.Status }).ToList() })
                    .ToListAsync(ct);

                foreach (var so in serviceOrders)
                {
                    if (!so.Dispatches.Any()) continue;
                    var childStatuses = so.Dispatches.Select(d => d.Status ?? "").ToList();
                    var conditionMet = EvaluateCollectionCondition(childStatuses, rule.ConditionOperator ?? "all_match", rule.ConditionValue ?? "");
                    string? expectedStatus = conditionMet ? rule.YesTargetStatus : rule.NoTargetStatus;
                    string? expectedEntityType = conditionMet ? rule.YesTargetEntityType : rule.NoTargetEntityType;
                    if (expectedStatus == null || so.Status == expectedStatus) continue;
                    if (expectedEntityType != null && expectedEntityType != "service_order") continue;

                    var serviceOrder = await db.ServiceOrders.FindAsync(new object[] { so.Id }, ct);
                    if (serviceOrder == null) continue;
                    var oldStatus = serviceOrder.Status;
                    serviceOrder.Status = expectedStatus;
                    serviceOrder.ModifiedDate = DateTime.UtcNow;
                    serviceOrder.ModifiedBy = "system-reconcile";
                    // Canonical counter definition (shared with ServiceOrderStatusCalculator) rather
                    // than the matched rule's own status list, which used to make it drift.
                    serviceOrder.CompletedDispatchCount = so.Dispatches
                        .Count(d => MyApi.Modules.ServiceOrders.Services.ServiceOrderStatusCalculator.IsCompletedDispatchStatus(d.Status));

                    if (expectedStatus == "in_progress" && !serviceOrder.ActualStartDate.HasValue) serviceOrder.ActualStartDate = DateTime.UtcNow;
                    if (expectedStatus == "technically_completed" || expectedStatus == "completed") { serviceOrder.TechnicallyCompletedAt ??= DateTime.UtcNow; serviceOrder.ActualCompletionDate ??= DateTime.UtcNow; }
                    await db.SaveChangesAsync(ct);
                    fixes++;
                    _logger.LogInformation("[WORKFLOW-RECONCILE] 🔧 Collection: SO #{Id} '{Old}' → '{New}' (condition={Met})", so.Id, oldStatus, expectedStatus, conditionMet ? "YES" : "NO");
                }
            }
            else if (parentEntityType == "sale" && (collectionName == "serviceorders" || collectionName == "service_orders"))
            {
                var sales = await db.Sales
                    .Where(s => s.Status != "closed" && s.Status != "cancelled")
                    .Select(s => new { s.Id, s.Status, ServiceOrders = db.ServiceOrders.Where(so => so.SaleId == s.Id.ToString()).Select(so => new { so.Id, so.Status }).ToList() })
                    .ToListAsync(ct);
                foreach (var sale in sales)
                {
                    if (!sale.ServiceOrders.Any()) continue;
                    var childStatuses = sale.ServiceOrders.Select(so => so.Status ?? "").ToList();
                    var conditionMet = EvaluateCollectionCondition(childStatuses, rule.ConditionOperator ?? "all_match", rule.ConditionValue ?? "");
                    string? expectedStatus = conditionMet ? rule.YesTargetStatus : rule.NoTargetStatus;
                    if (expectedStatus == null || sale.Status == expectedStatus) continue;
                    var saleEntity = await db.Sales.FindAsync(new object[] { sale.Id }, ct);
                    if (saleEntity == null) continue;
                    var oldStatus = saleEntity.Status;
                    saleEntity.Status = expectedStatus;
                    saleEntity.ModifiedDate = DateTime.UtcNow;
                    saleEntity.ModifiedBy = "system-reconcile";
                    await db.SaveChangesAsync(ct);
                    fixes++;
                    _logger.LogInformation("[WORKFLOW-RECONCILE] 🔧 Collection: Sale #{Id} '{Old}' → '{New}' (condition={Met})", sale.Id, oldStatus, expectedStatus, conditionMet ? "YES" : "NO");
                }
            }

            return fixes;
        }

        private async Task<int> ApplyDirectCascadeRuleAsync(ApplicationDbContext db, ReconciliationRule rule, CancellationToken ct)
        {
            int fixes = 0;
            if (rule.DirectTriggerEntityType == null || rule.YesTargetEntityType == null || rule.YesTargetStatus == null) return 0;

            if (rule.DirectTriggerEntityType == "dispatch" && rule.YesTargetEntityType == "service_order")
            {
                var soIds = await db.Dispatches.Where(d => !d.IsDeleted && d.Status == rule.DirectTriggerStatus && d.ServiceOrderId != null).Select(d => d.ServiceOrderId!.Value).Distinct().ToListAsync(ct);
                foreach (var soId in soIds)
                {
                    var so = await db.ServiceOrders.FindAsync(new object[] { soId }, ct);
                    if (so == null || so.Status == "closed" || so.Status == "cancelled") continue;
                    if (rule.DirectCondition == "ifNotAlreadyInProgress" && so.Status == rule.YesTargetStatus) continue;
                    if (GetStatusOrder("service_order", so.Status ?? "") >= GetStatusOrder("service_order", rule.YesTargetStatus)) continue;
                    var oldStatus = so.Status;
                    so.Status = rule.YesTargetStatus;
                    so.ModifiedDate = DateTime.UtcNow;
                    so.ModifiedBy = "system-reconcile";
                    if (rule.YesTargetStatus == "in_progress" && !so.ActualStartDate.HasValue) so.ActualStartDate = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    fixes++;
                    _logger.LogInformation("[WORKFLOW-RECONCILE] 🔧 Direct: SO #{Id} '{Old}' → '{New}'", soId, oldStatus, rule.YesTargetStatus);
                }
            }
            else if (rule.DirectTriggerEntityType == "service_order" && rule.YesTargetEntityType == "sale")
            {
                var sos = await db.ServiceOrders.Where(so => so.Status == rule.DirectTriggerStatus && so.SaleId != null).Select(so => new { so.Id, so.SaleId }).ToListAsync(ct);
                foreach (var soInfo in sos)
                {
                    if (!int.TryParse(soInfo.SaleId, out var saleId)) continue;
                    var sale = await db.Sales.FindAsync(new object[] { saleId }, ct);
                    if (sale == null || sale.Status == "closed" || sale.Status == "cancelled") continue;
                    if (GetStatusOrder("sale", sale.Status ?? "") >= GetStatusOrder("sale", rule.YesTargetStatus)) continue;
                    var oldStatus = sale.Status;
                    sale.Status = rule.YesTargetStatus;
                    sale.ModifiedDate = DateTime.UtcNow;
                    sale.ModifiedBy = "system-reconcile";
                    await db.SaveChangesAsync(ct);
                    fixes++;
                    _logger.LogInformation("[WORKFLOW-RECONCILE] 🔧 Direct: Sale #{Id} '{Old}' → '{New}'", saleId, oldStatus, rule.YesTargetStatus);
                }
            }
            else if (rule.DirectTriggerEntityType == "sale" && rule.YesTargetEntityType == "offer")
            {
                var sales = await db.Sales.Where(s => s.Status == rule.DirectTriggerStatus && s.OfferId != null).Select(s => new { s.Id, s.OfferId }).ToListAsync(ct);
                foreach (var saleInfo in sales)
                {
                    if (!int.TryParse(saleInfo.OfferId, out var offerId)) continue;
                    var offer = await db.Offers.FindAsync(new object[] { offerId }, ct);
                    if (offer == null || offer.Status == "cancelled" || offer.Status == "rejected") continue;
                    if (GetStatusOrder("offer", offer.Status ?? "") >= GetStatusOrder("offer", rule.YesTargetStatus)) continue;
                    var oldStatus = offer.Status;
                    offer.Status = rule.YesTargetStatus;
                    offer.ModifiedDate = DateTime.UtcNow;
                    offer.ModifiedBy = "system-reconcile";
                    await db.SaveChangesAsync(ct);
                    fixes++;
                    _logger.LogInformation("[WORKFLOW-RECONCILE] 🔧 Direct: Offer #{Id} '{Old}' → '{New}'", offerId, oldStatus, rule.YesTargetStatus);
                }
            }

            return fixes;
        }

        #endregion

        #region Condition Evaluation

        private bool EvaluateCollectionCondition(List<string> childStatuses, string operatorType, string expectedValue)
        {
            var expectedValues = expectedValue.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim().ToLower()).ToHashSet();
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
                "sale" => status switch { "created" => 0, "in_progress" => 1, "partially_invoiced" => 2, "invoiced" => 3, "closed" => 4, "cancelled" => -1, _ => 0 },
                "dispatch" => status switch
                {
                    // Aligned with src/config/entity-statuses/dispatch.config.ts
                    // Flow: assigned → confirmed → in_progress → completed
                    // (pending/planned are legacy pre-assigned states)
                    "pending" => 0, "planned" => 0, "assigned" => 1, "confirmed" => 2,
                    "in_progress" => 3, "completed" => 4,
                    "rejected" => -1, "cancelled" => -1, _ => 0
                },
                "offer" => status switch
                {
                    // Aligned with src/config/entity-statuses/offer.config.ts
                    // Primary statuses: draft, sent, modified, accepted, declined, cancelled
                    // Aliases (kept for legacy data): created→draft, pending/negotiation→sent, won→accepted, rejected/expired/lost→declined
                    "draft" => 0, "created" => 0,
                    "sent" => 1, "pending" => 1, "negotiation" => 1,
                    "modified" => 2,
                    "accepted" => 3, "won" => 3,
                    "declined" => -1, "rejected" => -1, "expired" => -1, "lost" => -1, "cancelled" => -1, _ => 0
                },
                "job" => status switch
                {
                    // Aligned with src/config/entity-statuses/job.config.ts
                    "unscheduled" => 0, "scheduled" => 1, "in_progress" => 2, "on_hold" => 2,
                    "completed" => 3, "cancelled" => -1, _ => 0
                },
                _ => 0
            };
        }

        #endregion

        #region Node Type Helpers

        private bool IsConditionNodeType(string type) { var t = type.ToLower(); return t.Contains("condition") || t.Contains("if-") || t.Contains("if_else"); }
        private bool IsUpdateStatusNodeType(string type) { var t = type.ToLower(); return t.Contains("update-") && t.Contains("status"); }
        private bool IsYesBranch(ReconcileEdge e) { var h = e.SourceHandle?.ToLower() ?? ""; var l = e.Label?.ToLower() ?? ""; return h == "yes" || h == "true" || l == "yes" || l == "true"; }
        private bool IsNoBranch(ReconcileEdge e) { var h = e.SourceHandle?.ToLower() ?? ""; var l = e.Label?.ToLower() ?? ""; return h == "no" || h == "false" || l == "no" || l == "false"; }
        private string? InferEntityTypeFromNodeType(string t) { t = t.ToLower(); if (t.Contains("service-order") || t.Contains("service_order")) return "service_order"; if (t.Contains("dispatch")) return "dispatch"; if (t.Contains("job")) return "job"; if (t.Contains("sale")) return "sale"; if (t.Contains("offer")) return "offer"; return null; }
        private string? ResolveEntityType(string p) { if (p.Contains("serviceorder") || p.Contains("service_order")) return "service_order"; if (p.Contains("sale")) return "sale"; if (p.Contains("offer")) return "offer"; if (p.Contains("dispatch")) return "dispatch"; if (p.Contains("job")) return "job"; return null; }

        #endregion

        #region Data Integrity Fixes

        private async Task<int> FixMissingStartDatesAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var sos = await db.ServiceOrders.Where(so => so.Status == "in_progress" && so.ActualStartDate == null).ToListAsync(ct);
            foreach (var so in sos) { so.ActualStartDate = DateTime.UtcNow; so.ModifiedDate = DateTime.UtcNow; so.ModifiedBy = "system-reconcile"; }
            if (sos.Any()) await db.SaveChangesAsync(ct);
            return sos.Count;
        }

        private async Task<int> FixCompletedDispatchCountsAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var mismatches = await db.ServiceOrders
                .Where(so => so.Status != "draft" && so.Status != "cancelled" && so.Status != "closed")
                .Select(so => new { so.Id, so.CompletedDispatchCount, Actual = db.Dispatches.Count(d => d.ServiceOrderId == so.Id && !d.IsDeleted && (d.Status == "technically_completed" || d.Status == "completed")) })
                .Where(x => x.CompletedDispatchCount != x.Actual).ToListAsync(ct);
            foreach (var m in mismatches) { var so = await db.ServiceOrders.FindAsync(new object[] { m.Id }, ct); if (so != null) { so.CompletedDispatchCount = m.Actual; so.ModifiedDate = DateTime.UtcNow; so.ModifiedBy = "system-reconcile"; } }
            if (mismatches.Any()) await db.SaveChangesAsync(ct);
            return mismatches.Count;
        }

        private async Task<int> FixMissingOfferIdsAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var sos = await db.ServiceOrders.Where(so => so.OfferId == null && so.SaleId != null).ToListAsync(ct);
            int fixes = 0;
            foreach (var so in sos) { if (int.TryParse(so.SaleId, out var saleId)) { var sale = await db.Sales.FindAsync(new object[] { saleId }, ct); if (sale?.OfferId != null) { so.OfferId = sale.OfferId; so.ModifiedDate = DateTime.UtcNow; so.ModifiedBy = "system-reconcile"; fixes++; } } }
            if (sos.Any()) await db.SaveChangesAsync(ct);
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
            public string? GetDataString(string key) { if (Data.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String) return el.GetString(); return null; }
            public string? GetConfigString(string key) { if (Data.TryGetValue("config", out var c) && c.ValueKind == JsonValueKind.Object && c.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String) return p.GetString(); return null; }
            public string? GetConditionDataString(string key)
            {
                if (Data.TryGetValue("config", out var c) && c.ValueKind == JsonValueKind.Object && c.TryGetProperty("conditionData", out var cd) && cd.ValueKind == JsonValueKind.Object && cd.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String) return p.GetString();
                if (Data.TryGetValue("conditionData", out var tc) && tc.ValueKind == JsonValueKind.Object && tc.TryGetProperty(key, out var tp) && tp.ValueKind == JsonValueKind.String) return tp.GetString();
                return null;
            }
        }
        private class ReconcileEdge { public string Id { get; set; } = ""; public string Source { get; set; } = ""; public string Target { get; set; } = ""; public string? SourceHandle { get; set; } public string? Label { get; set; } }
        private class ReconciliationRule
        {
            public bool IsCollectionRule { get; set; }
            public bool IsDirect { get; set; }
            public string? ConditionField { get; set; }
            public string? ConditionOperator { get; set; }
            public string? ConditionValue { get; set; }
            public string? ConditionCheckField { get; set; }
            public string? YesTargetEntityType { get; set; }
            public string? YesTargetStatus { get; set; }
            public string? NoTargetEntityType { get; set; }
            public string? NoTargetStatus { get; set; }
            public string? DirectTriggerEntityType { get; set; }
            public string? DirectTriggerStatus { get; set; }
            public string? DirectCondition { get; set; }
            public string Description { get; set; } = "";
        }

        #endregion

        private class EntityStatusInfo
        {
            public int Id { get; set; }
            public string Status { get; set; } = "";
            public object? Context { get; set; }
        }
    }
}
