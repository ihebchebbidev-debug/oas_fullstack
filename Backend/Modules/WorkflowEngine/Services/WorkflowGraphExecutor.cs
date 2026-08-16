using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.WorkflowEngine.Models;
using System.Diagnostics;
using System.Text.Json;

namespace MyApi.Modules.WorkflowEngine.Services
{
    public class WorkflowGraphExecutor : IWorkflowGraphExecutor
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<WorkflowGraphExecutor> _logger;
        private readonly IWorkflowNodeExecutor _nodeExecutor;
        private readonly IWorkflowNotificationService _notificationService;
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>Hard ceiling on node executions per run — catches infinite loops and graph cycles.</summary>
        private const int MaxExecutionSteps = 10_000;

        /// <summary>Hard ceiling on iterations for a single loop node, regardless of its config.</summary>
        private const int MaxLoopIterations = 1_000;



        public WorkflowGraphExecutor(
            ApplicationDbContext db,
            ILogger<WorkflowGraphExecutor> logger,
            IWorkflowNodeExecutor nodeExecutor,
            IWorkflowNotificationService notificationService,
            IServiceScopeFactory scopeFactory)
        {
            _db = db;
            _logger = logger;
            _nodeExecutor = nodeExecutor;
            _notificationService = notificationService;
            _scopeFactory = scopeFactory;
        }

        public async Task<GraphExecutionResult> ExecuteGraphAsync(
            int workflowId,
            int executionId,
            string startNodeId,
            WorkflowExecutionContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new GraphExecutionResult();

            try
            {
                // Load workflow definition
                var workflow = await _db.WorkflowDefinitions
                    .FirstOrDefaultAsync(w => w.Id == workflowId && !w.IsDeleted);

                if (workflow == null)
                {
                    return new GraphExecutionResult
                    {
                        Success = false,
                        FinalStatus = "failed",
                        Error = $"Workflow {workflowId} not found"
                    };
                }

                // Parse nodes and edges
                var nodes = ParseNodes(workflow.Nodes);
                var edges = ParseEdges(workflow.Edges);

                // P0 FIX: build label-slug → nodeId map so variable references like
                // {{welcome_email.subject}} (emitted by the frontend VariablePicker)
                // survive node ID changes AND node renames. Duplicates: last one wins,
                // but we log a warning so users can spot ambiguous references.
                foreach (var nd in nodes)
                {
                    if (string.IsNullOrWhiteSpace(nd.Label)) continue;
                    var slug = System.Text.RegularExpressions.Regex
                        .Replace(nd.Label.Trim(), @"\s+", "_").ToLowerInvariant();
                    if (string.IsNullOrEmpty(slug)) continue;
                    if (context.LabelSlugToNodeId.TryGetValue(slug, out var existing) && existing != nd.Id)
                    {
                        _logger.LogWarning(
                            "[WORKFLOW-GRAPH] Duplicate node label slug '{Slug}' — variable references may be ambiguous (nodes: {A}, {B})",
                            slug, existing, nd.Id);
                    }
                    context.LabelSlugToNodeId[slug] = nd.Id;
                }

                if (!nodes.Any())
                {
                    return new GraphExecutionResult
                    {
                        Success = false,
                        FinalStatus = "failed",
                        Error = "Workflow has no nodes"
                    };
                }

                // Workflow-level timeout: read maxDurationMinutes from a "workflow-config"
                // node or default to 60 min. Prevents runaway executions from hanging forever.
                var maxMinutes = 60;
                try
                {
                    var cfgNode = nodes.FirstOrDefault(n => n.Type == "workflow-config");
                    if (cfgNode != null
                        && cfgNode.Data.TryGetValue("maxDurationMinutes", out var mdEl)
                        && mdEl is JsonElement mdJsonEl
                        && mdJsonEl.ValueKind == JsonValueKind.Number)
                    {
                        maxMinutes = Math.Clamp(mdJsonEl.GetInt32(), 1, 1440);
                    }
                }
                catch { }
                var executionDeadline = context.StartedAt.AddMinutes(maxMinutes);

                _logger.LogInformation(
                    "Starting graph execution for workflow {WorkflowId} with {NodeCount} nodes and {EdgeCount} edges (timeout={Max}min)",
                    workflowId, nodes.Count, edges.Count, maxMinutes);

                // Build adjacency map for graph traversal
                var adjacencyMap = BuildAdjacencyMap(edges);
                var executed = new HashSet<string>();
                var queue = new Queue<string>();

                // Runaway guards. The timeout alone is not enough: a tight loop over
                // cheap nodes burns CPU and floods the log table for the full window,
                // and a misconfigured maxIterations (or a cycle in the saved graph)
                // can queue work faster than the deadline is checked.
                var steps = 0;

                // Start from the trigger node
                queue.Enqueue(startNodeId);

                while (queue.Count > 0)
                {
                    // Workflow timeout guard — fails the execution cleanly instead of hanging.
                    if (DateTime.UtcNow > executionDeadline)
                    {
                        result.FinalStatus = "failed";
                        result.Error = $"Workflow exceeded maximum execution time of {maxMinutes} minutes";
                        result.Success = false;
                        _logger.LogWarning(
                            "[WORKFLOW-GRAPH] Execution {ExecutionId} timed out after {Max}min — aborting remaining nodes",
                            executionId, maxMinutes);
                        break;
                    }

                    if (++steps > MaxExecutionSteps)
                    {
                        result.FinalStatus = "failed";
                        result.Error = $"Workflow exceeded the maximum of {MaxExecutionSteps} node executions (possible infinite loop)";
                        result.Success = false;
                        _logger.LogWarning(
                            "[WORKFLOW-GRAPH] Execution {ExecutionId} hit the step budget of {Max} — aborting (runaway loop?)",
                            executionId, MaxExecutionSteps);
                        break;
                    }

                    var currentNodeId = queue.Dequeue();


                    if (executed.Contains(currentNodeId))
                    {
                        _logger.LogDebug("[WORKFLOW-GRAPH] Skipping already executed node: {NodeId}", currentNodeId);
                        continue;
                    }

                    var node = nodes.FirstOrDefault(n => n.Id == currentNodeId);
                    if (node == null)
                    {
                        _logger.LogWarning("[WORKFLOW-GRAPH] Node {NodeId} not found in workflow definition!", currentNodeId);
                        continue;
                    }

                    _logger.LogInformation(
                        "[WORKFLOW-GRAPH] Executing node: {NodeId} (Type: {NodeType}, Label: {Label})",
                        node.Id, node.Type, node.Label);

                    executed.Add(currentNodeId);

                    // Notify that node is executing
                    await _notificationService.NotifyNodeExecutingAsync(
                        workflowId, executionId, node.Id, node.Type);

                    // Execute the node (with per-node retry/backoff from node config)
                    var nodeResult = await ExecuteNodeWithRetryAsync(executionId, node, context);

                    // Log the execution
                    await LogNodeExecutionAsync(executionId, node, nodeResult);

                    // Add to results
                    result.NodeResults.Add(new NodeExecutionSummary
                    {
                        NodeId = node.Id,
                        NodeType = node.Type,
                        Status = nodeResult.Status,
                        DurationMs = nodeResult.DurationMs,
                        Error = nodeResult.Error
                    });

                    if (nodeResult.Success)
                    {
                        result.NodesExecuted++;

                        // Store output for downstream nodes. We store under BOTH keys so
                        // the resolver can look up {{nodeId.field}} (via NodeOutputs[nodeId])
                        // and the historical "{nodeId}.output" bucket. Prior versions only
                        // stored the latter which meant no {{nodeId.x}} reference ever
                        // resolved (see WorkflowNodeExecutor.ResolveVariables).
                        context.NodeOutputs[node.Id] = nodeResult.Output;
                        context.NodeOutputs[$"{node.Id}.output"] = nodeResult.Output;
                        
                        // If entity was created, store in context
                        if (nodeResult.CreatedEntityId.HasValue)
                        {
                            context.Variables[$"created_{nodeResult.CreatedEntityType}_id"] = nodeResult.CreatedEntityId.Value;
                        }
                    }
                    else
                    {
                        result.NodesFailed++;
                    }

                    // Notify that node completed
                    await _notificationService.NotifyNodeCompletedAsync(
                        workflowId,
                        executionId,
                        node.Id,
                        node.Type,
                        nodeResult.Success,
                        nodeResult.Error,
                        JsonSerializer.Serialize(nodeResult.Output));

                    // Check if we should stop
                    if (nodeResult.ShouldStop)
                    {
                        _logger.LogInformation(
                            "Execution stopped at node {NodeId} with status {Status}",
                            node.Id, nodeResult.Status);

                        // BUG FIX: persist accumulated Variables back to execution.Context
                        // so that approval/delay resume can re-hydrate entity IDs and
                        // outputs from previously executed nodes. Without this, every
                        // downstream node after a pause loses created_*_id and similar.
                        await PersistContextAsync(executionId, context);

                        result.FinalStatus = nodeResult.Status;
                        break;
                    }

                    // Check for failure — but first try to route to an "error" edge (n8n-style catch path).
                    if (!nodeResult.Success)
                    {
                        if (adjacencyMap.TryGetValue(node.Id, out var errOutEdges))
                        {
                            var errorEdge = errOutEdges.FirstOrDefault(e =>
                                (e.SourceHandle?.ToLower() is "error" or "catch" or "fail") ||
                                (e.Label?.ToLower() is "error" or "catch" or "fail" or "on error"));

                            if (errorEdge != null)
                            {
                                _logger.LogInformation(
                                    "[WORKFLOW-GRAPH] Node {NodeId} failed but has error edge → routing to {Target}. Error: {Error}",
                                    node.Id, errorEdge.Target, nodeResult.Error);
                                context.Variables["last_error"]       = nodeResult.Error ?? "";
                                context.Variables["last_failed_node"] = node.Id;
                                if (!executed.Contains(errorEdge.Target))
                                    queue.Enqueue(errorEdge.Target);
                                continue; // skip normal "failed" break
                            }
                        }

                        _logger.LogWarning("Node {NodeId} failed: {Error}", node.Id, nodeResult.Error);
                        result.FinalStatus = "failed";
                        result.Error = nodeResult.Error;
                        break;
                    }

                    // Determine next nodes based on node type and result
                    var nextNodes = GetNextNodes(node, nodeResult, edges, adjacencyMap, context);
                    
                    _logger.LogInformation(
                        "[WORKFLOW-GRAPH] Node {NodeId} completed. Next nodes: [{NextNodes}] (Branch: {Branch})",
                        node.Id, 
                        string.Join(", ", nextNodes),
                        nodeResult.SelectedBranch ?? "N/A");

                    // PARALLEL EXECUTION: if this node is a Parallel/fork node, run each
                    // outgoing branch concurrently on its own DbContext scope, then merge
                    // results back into the aggregate and mark all branch-touched nodes as
                    // executed so the main queue doesn't re-run them.
                    if (node.Type.Contains("parallel") && nextNodes.Count > 1)
                    {
                        var maxConcurrency = 5;
                        var waitForAll = true;
                        if (context.Variables.TryGetValue($"{node.Id}_maxConcurrency", out var mc) && mc is int mci) maxConcurrency = mci;
                        if (context.Variables.TryGetValue($"{node.Id}_waitForAll", out var wfa) && wfa is bool wfab) waitForAll = wfab;
                        if (maxConcurrency < 1) maxConcurrency = 1;

                        _logger.LogInformation(
                            "[WORKFLOW-GRAPH] Parallel fork at {NodeId}: {Count} branches, maxConcurrency={Max}, waitForAll={Wait}",
                            node.Id, nextNodes.Count, maxConcurrency, waitForAll);

                        using var sem = new SemaphoreSlim(maxConcurrency);
                        var branchTasks = nextNodes.Select(async startId =>
                        {
                            await sem.WaitAsync();
                            try
                            {
                                // Snapshot variables for this branch so concurrent mutation is safe.
                                var branchCtx = new WorkflowExecutionContext
                                {
                                    WorkflowId = context.WorkflowId,
                                    ExecutionId = context.ExecutionId,
                                    TriggerEntityType = context.TriggerEntityType,
                                    TriggerEntityId = context.TriggerEntityId,
                                    UserId = context.UserId,
                                    Variables = new Dictionary<string, object?>(context.Variables),
                                    NodeOutputs = new Dictionary<string, object?>(context.NodeOutputs),
                                    StartedAt = context.StartedAt
                                };

                                // Each branch gets its own DI scope (and DbContext + executor)
                                // because EF Core DbContext is not thread-safe.
                                using var scope = _scopeFactory.CreateScope();
                                var branchExecutor = scope.ServiceProvider.GetRequiredService<IWorkflowGraphExecutor>();
                                return new { StartId = startId, Result = await branchExecutor.ExecuteGraphAsync(workflowId, executionId, startId, branchCtx), Ctx = branchCtx };
                            }
                            finally { sem.Release(); }
                        }).ToList();

                        var branchResults = waitForAll
                            ? await Task.WhenAll(branchTasks)
                            : new[] { await await Task.WhenAny(branchTasks) };

                        // Merge branch results and variables back.
                        foreach (var br in branchResults)
                        {
                            result.NodesExecuted += br.Result.NodesExecuted;
                            result.NodesFailed   += br.Result.NodesFailed;
                            result.NodesSkipped  += br.Result.NodesSkipped;
                            result.NodeResults.AddRange(br.Result.NodeResults);
                            foreach (var kvp in br.Ctx.Variables) context.Variables[kvp.Key] = kvp.Value;
                            foreach (var kvp in br.Ctx.NodeOutputs) context.NodeOutputs[kvp.Key] = kvp.Value;

                            if (!br.Result.Success)
                            {
                                result.Success = false;
                                result.FinalStatus = br.Result.FinalStatus;
                                result.Error = br.Result.Error;
                            }
                            else if (br.Result.FinalStatus != "completed" && string.IsNullOrEmpty(result.FinalStatus))
                            {
                                result.FinalStatus = br.Result.FinalStatus;
                            }
                        }

                        // Mark branch start-nodes as executed in the outer traversal so the
                        // main queue doesn't run them sequentially after the fork.
                        foreach (var startId in nextNodes) executed.Add(startId);

                        // If any branch failed and we were waiting for all, stop the fork.
                        if (!result.Success) break;

                        // Skip the normal enqueue path — parallel branches handled it.
                        continue;
                    }

                    foreach (var nextNodeId in nextNodes)
                    {
                        if (!executed.Contains(nextNodeId))
                        {
                            _logger.LogDebug("[WORKFLOW-GRAPH] Queueing next node: {NextNodeId}", nextNodeId);
                            queue.Enqueue(nextNodeId);
                        }
                    }
                    
                    // Handle loop nodes: if node is a loop and has iterations remaining,
                    // re-queue its children for the next iteration
                    if (node.Type.Contains("loop") && nodeResult.Success)
                    {
                        var iterKey = $"{node.Id}_iteration";
                        var maxKey = $"{node.Id}_maxIterations";
                        var currentIter = 0;
                        var maxIter = 1;
                        
                        if (context.Variables.TryGetValue(iterKey, out var iterVal))
                            currentIter = iterVal is int i ? i : (int)(iterVal is double d ? d : 0);
                        if (context.Variables.TryGetValue(maxKey, out var maxVal))
                            maxIter = maxVal is int mi ? mi : (int)(maxVal is double md ? md : 1);

                        // Clamp: maxIterations comes straight from user-editable node config,
                        // so a typo (or a hostile value) must not be able to spin forever.
                        if (maxIter > MaxLoopIterations)
                        {
                            _logger.LogWarning(
                                "[WORKFLOW-GRAPH] Loop node {NodeId} requested {Requested} iterations — clamped to {Max}",
                                node.Id, maxIter, MaxLoopIterations);
                            maxIter = MaxLoopIterations;
                        }
                        if (maxIter < 1) maxIter = 1;

                        
                        currentIter++;
                        context.Variables[iterKey] = currentIter;
                        
                        if (currentIter < maxIter)
                        {
                            // Re-allow child nodes to be executed again
                            foreach (var nextNodeId in nextNodes)
                            {
                                executed.Remove(nextNodeId);
                                queue.Enqueue(nextNodeId);
                            }
                            // BUG FIX: also re-queue the loop node itself so the iteration
                            // counter is re-evaluated after the body finishes. Previously
                            // the node was only removed from `executed`, but since edges
                            // point forward (DAG), nothing pointed back to it, so loops
                            // executed exactly once regardless of maxIterations.
                            executed.Remove(node.Id);
                            queue.Enqueue(node.Id);
                        }
                    }
                }
                
                // Mark skipped nodes
                result.NodesSkipped = nodes.Count - executed.Count;
                result.Success = result.NodesFailed == 0;

                // Set final status to "completed" if no node explicitly set it and there were no failures
                if (result.Success && string.IsNullOrEmpty(result.FinalStatus))
                {
                    result.FinalStatus = "completed";
                }

                _logger.LogInformation(
                    "Graph execution finished with status '{Status}': {Executed} executed, {Failed} failed, {Skipped} skipped",
                    result.FinalStatus, result.NodesExecuted, result.NodesFailed, result.NodesSkipped);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow graph");
                result.Success = false;
                result.FinalStatus = "failed";
                result.Error = ex.Message;
            }

            stopwatch.Stop();
            result.TotalDurationMs = (int)stopwatch.ElapsedMilliseconds;

            return result;
        }

        /// <summary>
        /// Executes a node with configurable retry and exponential backoff.
        /// Reads retryCount (0–10) and retryDelayMs (100–60000) from node.Data.
        /// Pause/stop results (ShouldStop = true) bypass retry immediately.
        /// </summary>
        private async Task<NodeExecutionResult> ExecuteNodeWithRetryAsync(
            int executionId,
            WorkflowNode node,
            WorkflowExecutionContext context)
        {
            var maxRetries = 0;
            var retryDelayMs = 1000;

            try
            {
                if (node.Data.TryGetValue("retryCount", out var rcEl) && rcEl is JsonElement rcJson
                    && rcJson.ValueKind == JsonValueKind.Number)
                    maxRetries = Math.Clamp(rcJson.GetInt32(), 0, 10);

                if (node.Data.TryGetValue("retryDelayMs", out var rdEl) && rdEl is JsonElement rdJson
                    && rdJson.ValueKind == JsonValueKind.Number)
                    retryDelayMs = Math.Clamp(rdJson.GetInt32(), 100, 60_000);
            }
            catch { /* ignore bad config */ }

            NodeExecutionResult? lastResult = null;

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    // Exponential backoff: 1s, 2s, 4s, 8s …
                    var backoffMs = Math.Min(retryDelayMs * (int)Math.Pow(2, attempt - 1), 60_000);
                    _logger.LogInformation(
                        "[WORKFLOW-GRAPH] Node {NodeId} retry {Attempt}/{Max} after {Backoff}ms backoff",
                        node.Id, attempt, maxRetries, backoffMs);
                    await Task.Delay(backoffMs);
                }

                lastResult = await _nodeExecutor.ExecuteNodeAsync(executionId, node, context);

                // Don't retry pause/stop signals or successful results.
                if (lastResult.Success || lastResult.ShouldStop) break;

                if (attempt < maxRetries)
                {
                    _logger.LogWarning(
                        "[WORKFLOW-GRAPH] Node {NodeId} failed (attempt {Attempt}/{Total}): {Error}",
                        node.Id, attempt + 1, maxRetries + 1, lastResult.Error);
                }
            }

            return lastResult!;
        }

        private async Task PersistContextAsync(int executionId, WorkflowExecutionContext context)
        {
            try
            {
                var exec = await _db.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == executionId);
                if (exec == null) return;
                exec.Context = JsonSerializer.Serialize(context.Variables);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WORKFLOW-GRAPH] Failed to persist context for execution {ExecutionId}", executionId);
            }
        }

        public async Task<GraphExecutionResult> ResumeAfterNodeAsync(
            int workflowId,
            int executionId,
            string pausedNodeId,
            WorkflowExecutionContext context)
        {
            // Load the workflow to find successors of the paused node
            var workflow = await _db.WorkflowDefinitions
                .FirstOrDefaultAsync(w => w.Id == workflowId && !w.IsDeleted);

            if (workflow == null)
            {
                return new GraphExecutionResult
                {
                    Success = false,
                    FinalStatus = "failed",
                    Error = $"Workflow {workflowId} not found"
                };
            }

            var edges = ParseEdges(workflow.Edges);
            var successors = edges
                .Where(e => e.Source == pausedNodeId)
                .Select(e => e.Target)
                .Distinct()
                .ToList();

            if (successors.Count == 0)
            {
                _logger.LogInformation(
                    "[WORKFLOW-RESUME] No successors after node {NodeId}; marking execution {ExecutionId} completed",
                    pausedNodeId, executionId);
                return new GraphExecutionResult { Success = true, FinalStatus = "completed" };
            }

            var aggregate = new GraphExecutionResult { Success = true, FinalStatus = "completed" };
            foreach (var startNodeId in successors)
            {
                var branchResult = await ExecuteGraphAsync(workflowId, executionId, startNodeId, context);
                aggregate.NodesExecuted += branchResult.NodesExecuted;
                aggregate.NodesFailed += branchResult.NodesFailed;
                aggregate.NodesSkipped += branchResult.NodesSkipped;
                aggregate.TotalDurationMs += branchResult.TotalDurationMs;
                aggregate.NodeResults.AddRange(branchResult.NodeResults);

                if (!branchResult.Success)
                {
                    aggregate.Success = false;
                    aggregate.FinalStatus = branchResult.FinalStatus;
                    aggregate.Error = branchResult.Error;
                    break;
                }

                // If a downstream branch paused again (e.g. another approval), propagate that status.
                if (branchResult.FinalStatus != "completed")
                {
                    aggregate.FinalStatus = branchResult.FinalStatus;
                }
            }

            return aggregate;
        }

        private List<WorkflowNode> ParseNodes(string nodesJson)
        {
            try
            {
                var jsonNodes = JsonSerializer.Deserialize<List<JsonElement>>(nodesJson);
                if (jsonNodes == null) return new List<WorkflowNode>();

                return jsonNodes.Select(ParseNodeFromJson).Where(n => n != null).Cast<WorkflowNode>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing nodes JSON");
                return new List<WorkflowNode>();
            }
        }

        private WorkflowNode? ParseNodeFromJson(JsonElement element)
        {
            try
            {
                var node = new WorkflowNode
                {
                    Id = element.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                    // Read React Flow type as fallback
                    Type = element.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "" : "",
                    Label = ""
                };

                if (element.TryGetProperty("position", out var posEl))
                {
                    node.Position = new NodePosition
                    {
                        X = posEl.TryGetProperty("x", out var xEl) ? xEl.GetDouble() : 0,
                        Y = posEl.TryGetProperty("y", out var yEl) ? yEl.GetDouble() : 0
                    };
                }

                if (element.TryGetProperty("data", out var dataEl))
                {
                    node.Label = dataEl.TryGetProperty("label", out var labelEl) ? labelEl.GetString() ?? "" : "";
                    
                    // CRITICAL: Use business type from data.type instead of React Flow node type
                    // React Flow types are "entityTrigger", "entityAction", "conditionNode", "n8nNode"
                    // Business types are "offer-status-trigger", "sale", "if-else", etc.
                    if (dataEl.TryGetProperty("type", out var dataTypeEl))
                    {
                        var businessType = dataTypeEl.GetString();
                        if (!string.IsNullOrEmpty(businessType))
                        {
                            node.Type = businessType;
                        }
                    }
                    
                    // Parse all data properties
                    foreach (var prop in dataEl.EnumerateObject())
                    {
                        node.Data[prop.Name] = prop.Value.Clone();
                    }
                }

                return node;
            }
            catch
            {
                return null;
            }
        }

        private List<WorkflowEdge> ParseEdges(string edgesJson)
        {
            try
            {
                var jsonEdges = JsonSerializer.Deserialize<List<JsonElement>>(edgesJson);
                if (jsonEdges == null) return new List<WorkflowEdge>();

                return jsonEdges.Select(ParseEdgeFromJson).Where(e => e != null).Cast<WorkflowEdge>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing edges JSON");
                return new List<WorkflowEdge>();
            }
        }

        private WorkflowEdge? ParseEdgeFromJson(JsonElement element)
        {
            try
            {
                return new WorkflowEdge
                {
                    Id = element.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                    Source = element.TryGetProperty("source", out var srcEl) ? srcEl.GetString() ?? "" : "",
                    Target = element.TryGetProperty("target", out var tgtEl) ? tgtEl.GetString() ?? "" : "",
                    SourceHandle = element.TryGetProperty("sourceHandle", out var srcHEl) ? srcHEl.GetString() : null,
                    TargetHandle = element.TryGetProperty("targetHandle", out var tgtHEl) ? tgtHEl.GetString() : null,
                    Label = element.TryGetProperty("label", out var lblEl) ? lblEl.GetString() : null
                };
            }
            catch
            {
                return null;
            }
        }

        private Dictionary<string, List<WorkflowEdge>> BuildAdjacencyMap(List<WorkflowEdge> edges)
        {
            var map = new Dictionary<string, List<WorkflowEdge>>();

            foreach (var edge in edges)
            {
                if (!map.ContainsKey(edge.Source))
                {
                    map[edge.Source] = new List<WorkflowEdge>();
                }
                map[edge.Source].Add(edge);
            }

            return map;
        }

        private List<string> GetNextNodes(
            WorkflowNode node,
            NodeExecutionResult result,
            List<WorkflowEdge> edges,
            Dictionary<string, List<WorkflowEdge>> adjacencyMap,
            WorkflowExecutionContext? context = null)
        {
            var nextNodes = new List<string>();

            if (!adjacencyMap.TryGetValue(node.Id, out var outgoingEdges))
            {
                return nextNodes;
            }

            // Approval nodes: route to "approved"/"rejected" edge based on approval_result
            if (node.Type.Contains("approval"))
            {
                var branch = "approved"; // default: positive branch
                if (context?.Variables.TryGetValue("_approval_branch", out var ab) == true && ab is string abs)
                    branch = abs.ToLower();

                foreach (var edge in outgoingEdges)
                {
                    var h = edge.SourceHandle?.ToLower() ?? "";
                    var l = edge.Label?.ToLower() ?? "";
                    if (h == branch || l == branch) nextNodes.Add(edge.Target);
                    // "approved" also matches "yes"/"true"; "rejected" also matches "no"/"false"
                    else if (branch == "approved" && (h == "yes" || h == "true" || l == "yes" || l == "true")) nextNodes.Add(edge.Target);
                    else if (branch == "rejected" && (h == "no" || h == "false" || l == "no" || l == "false")) nextNodes.Add(edge.Target);
                }
                // If still no match, fall back to all unlabelled edges
                if (!nextNodes.Any())
                    nextNodes.AddRange(outgoingEdges.Where(e => string.IsNullOrEmpty(e.SourceHandle) && string.IsNullOrEmpty(e.Label)).Select(e => e.Target));
                return nextNodes;
            }

            // For condition nodes, filter by branch
            if (node.Type.Contains("condition") || node.Type.Contains("if-"))
            {
                var selectedBranch = result.SelectedBranch?.ToLower() ?? "yes";
                
                // Normalize to yes/no for consistent matching
                if (selectedBranch == "true") selectedBranch = "yes";
                if (selectedBranch == "false") selectedBranch = "no";
                
                foreach (var edge in outgoingEdges)
                {
                    var handle = edge.SourceHandle?.ToLower() ?? "";
                    var label = edge.Label?.ToLower() ?? "";

                    // Match by handle or label (now using normalized yes/no)
                    if (handle == selectedBranch || label == selectedBranch)
                    {
                        nextNodes.Add(edge.Target);
                    }
                }

                // BUG FIX: previously, if no edge matched and branch == "yes" we followed
                // ALL outgoing edges, which caused both YES and NO branches to fire when
                // edges lacked a sourceHandle. Now we only fall back to UNLABELED edges
                // (handle == null AND label == null), which preserves single-edge flows
                // while never bleeding into the opposite branch.
                if (!nextNodes.Any())
                {
                    foreach (var edge in outgoingEdges)
                    {
                        var hasHandle = !string.IsNullOrEmpty(edge.SourceHandle);
                        var hasLabel = !string.IsNullOrEmpty(edge.Label);
                        if (!hasHandle && !hasLabel)
                        {
                            nextNodes.Add(edge.Target);
                        }
                    }
                    if (!nextNodes.Any())
                    {
                        _logger.LogWarning(
                            "[WORKFLOW-GRAPH] Condition node {NodeId}: branch '{Branch}' matched no outgoing edge; halting branch.",
                            node.Id, selectedBranch);
                    }
                }
            }
            // For switch nodes, filter by case
            else if (node.Type.Contains("switch"))
            {
                var selectedCase = result.SelectedCase?.ToLower() ?? "default";
                var matchedCase = false;

                foreach (var edge in outgoingEdges)
                {
                    var handle = edge.SourceHandle?.ToLower() ?? "";
                    var label = edge.Label?.ToLower() ?? "";

                    // Match the specific case handle (handle ID = case value)
                    if (handle == selectedCase || label == selectedCase)
                    {
                        nextNodes.Add(edge.Target);
                        matchedCase = true;
                    }
                }

                // Only follow default if no specific case matched
                if (!matchedCase)
                {
                    foreach (var edge in outgoingEdges)
                    {
                        var handle = edge.SourceHandle?.ToLower() ?? "";
                        if (handle == "default")
                        {
                            nextNodes.Add(edge.Target);
                        }
                    }

                    // Fix §6.1: match the condition-node path — warn when nothing routes
                    // instead of silently dead-ending the branch (which was asymmetric with
                    // condition nodes and made "why did this workflow stop?" undebuggable).
                    if (!nextNodes.Any())
                    {
                        _logger.LogWarning(
                            "[WORKFLOW-GRAPH] Switch node {NodeId}: case '{Case}' matched no edge and no 'default' edge exists; halting branch.",
                            node.Id, selectedCase);
                    }
                }
            }
            // For regular nodes, follow all outgoing edges
            else
            {
                nextNodes.AddRange(outgoingEdges.Select(e => e.Target));
            }

            return nextNodes;
        }

        private async Task LogNodeExecutionAsync(int executionId, WorkflowNode node, NodeExecutionResult result)
        {
            try
            {
                var log = new WorkflowExecutionLog
                {
                    ExecutionId = executionId,
                    NodeId = node.Id,
                    NodeType = node.Type,
                    Status = result.Status,
                    Input = JsonSerializer.Serialize(node.Data),
                    Output = JsonSerializer.Serialize(result.Output),
                    Error = result.Error,
                    Duration = result.DurationMs,
                    Timestamp = DateTime.UtcNow
                };

                _db.WorkflowExecutionLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log node execution for {NodeId}", node.Id);
            }
        }
    }
}
