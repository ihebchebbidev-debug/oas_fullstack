using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.WorkflowEngine.DTOs;
using MyApi.Modules.WorkflowEngine.Models;
using System.Text.Json;

namespace MyApi.Modules.WorkflowEngine.Services
{
    public class WorkflowEngineService : IWorkflowEngineService
    {
        private readonly ApplicationDbContext _db;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowEngineService>? _logger;

        public WorkflowEngineService(
            ApplicationDbContext db,
            IServiceProvider serviceProvider,
            ILogger<WorkflowEngineService>? logger = null)
        {
            _db = db;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Runs the workflow graph for the given execution and updates final status.
        /// Resolved lazily via IServiceProvider to avoid a circular dependency between
        /// IWorkflowEngineService and IWorkflowGraphExecutor.
        /// </summary>
        private async Task RunGraphAsync(
            WorkflowExecution execution,
            string startNodeId,
            Dictionary<string, object?> variables)
        {
            try
            {
                var graphExecutor = _serviceProvider.GetRequiredService<IWorkflowGraphExecutor>();
                var ctx = new WorkflowExecutionContext
                {
                    WorkflowId = execution.WorkflowId,
                    ExecutionId = execution.Id,
                    TriggerEntityType = execution.TriggerEntityType,
                    TriggerEntityId = execution.TriggerEntityId,
                    UserId = execution.TriggeredBy,
                    Variables = variables,
                };

                var result = await graphExecutor.ExecuteGraphAsync(
                    execution.WorkflowId,
                    execution.Id,
                    startNodeId,
                    ctx);

                execution.Status = result.FinalStatus;
                execution.Error = result.Error;
                if (result.FinalStatus == "completed" || result.FinalStatus == "failed")
                {
                    execution.CompletedAt = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Graph execution failed for execution {ExecutionId} on workflow {WorkflowId}",
                    execution.Id, execution.WorkflowId);
                execution.Status = "failed";
                execution.Error = ex.Message;
                execution.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// PERF: never Include(w => w.Executions) here — the execution history grows
        /// unbounded and the DTO only needs a count. Load the counts with a single
        /// grouped query instead.
        /// </summary>
        private async Task<Dictionary<int, int>> GetExecutionCountsAsync(IEnumerable<int> workflowIds)
        {
            var ids = workflowIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, int>();

            return await _db.WorkflowExecutions
                .Where(e => ids.Contains(e.WorkflowId))
                .GroupBy(e => e.WorkflowId)
                .Select(g => new { WorkflowId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.WorkflowId, x => x.Count);
        }

        private async Task<int> GetExecutionCountAsync(int workflowId)
            => await _db.WorkflowExecutions.CountAsync(e => e.WorkflowId == workflowId);

        public async Task<IEnumerable<WorkflowDefinitionDto>> GetAllWorkflowsAsync()
        {
            var workflows = await _db.WorkflowDefinitions
                .Where(w => !w.IsDeleted)
                .Include(w => w.Triggers)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();

            var counts = await GetExecutionCountsAsync(workflows.Select(w => w.Id));

            return workflows.Select(w => MapToDto(w, counts.TryGetValue(w.Id, out var c) ? c : 0));
        }

        public async Task<WorkflowDefinitionDto?> GetWorkflowByIdAsync(int id)
        {
            var workflow = await _db.WorkflowDefinitions
                .Include(w => w.Triggers)
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);

            return workflow == null ? null : MapToDto(workflow, await GetExecutionCountAsync(workflow.Id));
        }


        /// <summary>
        /// Gets the default workflow (Name = 'Default Business Workflow', IsActive = true)
        /// This workflow is application-wide and always running
        /// </summary>
        public async Task<WorkflowDefinitionDto?> GetDefaultWorkflowAsync()
        {
            var workflow = await _db.WorkflowDefinitions
                .Include(w => w.Triggers)
                .FirstOrDefaultAsync(w => 
                    w.Name == "Default Business Workflow" 
                    && w.IsActive 
                    && !w.IsDeleted);

            return workflow == null ? null : MapToDto(workflow, await GetExecutionCountAsync(workflow.Id));
        }

        public async Task<WorkflowDefinitionDto> CreateWorkflowAsync(CreateWorkflowDto dto, string createdBy)
        {
            var nodesJson = JsonSerializer.Serialize(dto.Nodes);
            var edgesJson = JsonSerializer.Serialize(dto.Edges);

            // RELIABILITY: reject obviously broken graphs at save-time so users can't
            // build workflows that will never run (no nodes, no trigger, edges that
            // point to missing nodes, etc.). Throws InvalidOperationException.
            ValidateGraphOrThrow(nodesJson, edgesJson);

            var workflow = new WorkflowDefinition
            {
                Name = dto.Name,
                Description = dto.Description,
                Nodes = nodesJson,
                Edges = edgesJson,
                IsActive = dto.IsActive,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _db.WorkflowDefinitions.Add(workflow);
            await _db.SaveChangesAsync();

            // Extract and register triggers from nodes
            await ExtractAndRegisterTriggersAsync(workflow);

            return MapToDto(workflow);
        }

        public async Task<WorkflowDefinitionDto?> UpdateWorkflowAsync(int id, UpdateWorkflowDto dto, string modifiedBy)
        {
            var workflow = await _db.WorkflowDefinitions
                .Include(w => w.Triggers)
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);

            if (workflow == null) return null;

            if (dto.Name != null) workflow.Name = dto.Name;
            if (dto.Description != null) workflow.Description = dto.Description;
            if (dto.Nodes != null || dto.Edges != null)
            {
                var nextNodes = dto.Nodes != null ? JsonSerializer.Serialize(dto.Nodes) : workflow.Nodes;
                var nextEdges = dto.Edges != null ? JsonSerializer.Serialize(dto.Edges) : workflow.Edges;
                // RELIABILITY: validate the resulting graph before persisting so the user
                // never ends up with a workflow that cannot fire (or fires nowhere).
                ValidateGraphOrThrow(nextNodes, nextEdges);
                workflow.Nodes = nextNodes;
                workflow.Edges = nextEdges;
            }
            if (dto.IsActive.HasValue) workflow.IsActive = dto.IsActive.Value;

            workflow.UpdatedAt = DateTime.UtcNow;
            workflow.ModifiedBy = modifiedBy;
            workflow.Version++;

            await _db.SaveChangesAsync();

            // Re-extract and register triggers if nodes changed.
            // Wrapped in a transaction: if re-registration fails after deletion,
            // the rollback restores the old triggers instead of leaving none.
            if (dto.Nodes != null)
            {
                // Wrap in execution strategy to be compatible with EnableRetryOnFailure
                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _db.Database.BeginTransactionAsync();
                    try
                    {
                        _db.WorkflowTriggers.RemoveRange(workflow.Triggers);
                        await _db.SaveChangesAsync();
                        await ExtractAndRegisterTriggersAsync(workflow);
                        await tx.CommitAsync();
                    }
                    catch
                    {
                        await tx.RollbackAsync();
                        throw;
                    }
                });
            }

            return MapToDto(workflow);
        }

        public async Task<bool> DeleteWorkflowAsync(int id)
        {
            var workflow = await _db.WorkflowDefinitions.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);
            if (workflow == null) return false;

            workflow.IsDeleted = true;
            workflow.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ActivateWorkflowAsync(int id)
        {
            var workflow = await _db.WorkflowDefinitions.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);
            if (workflow == null) return false;

            workflow.IsActive = true;
            workflow.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeactivateWorkflowAsync(int id)
        {
            var workflow = await _db.WorkflowDefinitions.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);
            if (workflow == null) return false;

            workflow.IsActive = false;
            workflow.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return true;
        }

        // ─── Version Management ─────────────────────────────────────────────

        /// <summary>
        /// Creates a draft copy of an existing workflow for safe editing.
        /// The original workflow remains active and untouched.
        /// </summary>
        public async Task<WorkflowDefinitionDto> CreateDraftFromWorkflowAsync(int id, string createdBy)
        {
            var source = await _db.WorkflowDefinitions
                .Include(w => w.Triggers)
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);

            if (source == null)
                throw new KeyNotFoundException($"Workflow {id} not found");

            // Create a draft copy with incremented version
            var draft = new WorkflowDefinition
            {
                Name = $"{source.Name} (Draft v{source.Version + 1})",
                Description = source.Description,
                Nodes = source.Nodes,   // Deep-copy the JSON as-is
                Edges = source.Edges,
                IsActive = false,       // Drafts are never active
                Version = source.Version + 1,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _db.WorkflowDefinitions.Add(draft);
            await _db.SaveChangesAsync();

            // Copy triggers so the draft has the same trigger configuration
            if (source.Triggers != null && source.Triggers.Any())
            {
                foreach (var srcTrigger in source.Triggers)
                {
                    _db.WorkflowTriggers.Add(new WorkflowTrigger
                    {
                        WorkflowId = draft.Id,
                        NodeId = srcTrigger.NodeId,
                        EntityType = srcTrigger.EntityType,
                        FromStatus = srcTrigger.FromStatus,
                        ToStatus = srcTrigger.ToStatus,
                        IsActive = false, // Draft triggers are inactive
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _db.SaveChangesAsync();
            }

            // Reload with navigation properties
            var result = await _db.WorkflowDefinitions
                .Include(w => w.Triggers)
                .FirstAsync(w => w.Id == draft.Id);

            return MapToDto(result, await GetExecutionCountAsync(result.Id));
        }

        /// <summary>
        /// Promotes a draft/inactive workflow to active.
        /// Deactivates any other active workflow with a similar base name to prevent conflicts.
        /// </summary>
        public async Task<WorkflowDefinitionDto?> PromoteWorkflowAsync(int id, string modifiedBy)
        {
            var workflow = await _db.WorkflowDefinitions
                .Include(w => w.Triggers)
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);

            if (workflow == null) return null;

            // Already active? Nothing to do
            if (workflow.IsActive)
            {
                return MapToDto(workflow, await GetExecutionCountAsync(workflow.Id));
            }

            // Determine the base name (strip " (Draft vN)" suffix)
            var baseName = workflow.Name;
            var draftIdx = baseName.IndexOf(" (Draft", StringComparison.OrdinalIgnoreCase);
            if (draftIdx > 0) baseName = baseName.Substring(0, draftIdx);

            // Deactivate any currently active workflows with the same base name
            var activeConflicts = await _db.WorkflowDefinitions
                .Where(w => !w.IsDeleted && w.IsActive && w.Id != id
                    && (w.Name == baseName || w.Name.StartsWith(baseName + " (")))
                .ToListAsync();

            foreach (var conflict in activeConflicts)
            {
                conflict.IsActive = false;
                conflict.UpdatedAt = DateTime.UtcNow;
                conflict.ModifiedBy = modifiedBy;
            }

            // Promote this workflow
            workflow.IsActive = true;
            workflow.Name = baseName; // Clean the name (remove Draft suffix)
            workflow.UpdatedAt = DateTime.UtcNow;
            workflow.ModifiedBy = modifiedBy;

            // Activate its triggers
            if (workflow.Triggers != null)
            {
                foreach (var trigger in workflow.Triggers)
                {
                    trigger.IsActive = true;
                }
            }

            await _db.SaveChangesAsync();

            // Re-extract triggers in a transaction so a registration failure
            // cannot leave the promoted workflow with zero active triggers.
            // Wrap in execution strategy to be compatible with EnableRetryOnFailure
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                try
                {
                    _db.WorkflowTriggers.RemoveRange(workflow.Triggers ?? Enumerable.Empty<WorkflowTrigger>());
                    await _db.SaveChangesAsync();
                    await ExtractAndRegisterTriggersAsync(workflow);
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            // Reload
            var result = await _db.WorkflowDefinitions
                .Include(w => w.Triggers)
                .FirstAsync(w => w.Id == id);

            return MapToDto(result, await GetExecutionCountAsync(result.Id));
        }

        /// <summary>
        /// Archives a workflow (soft-delete that preserves history).
        /// Cannot archive a workflow that has running executions.
        /// </summary>
        public async Task<bool> ArchiveWorkflowAsync(int id, string modifiedBy)
        {
            var workflow = await _db.WorkflowDefinitions
                .Include(w => w.Triggers)
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);

            if (workflow == null) return false;

            // Safety: don't archive if there are running executions.
            // BUG FIX (BUG-6): also block when executions are parked on a delay node
            // (waiting_delay); archiving them silently abandons in-flight work.
            // PERF: ask the database instead of materialising the whole history.
            var hasRunning = await _db.WorkflowExecutions.AnyAsync(e =>
                e.WorkflowId == workflow.Id && (
                    e.Status == "running" ||
                    e.Status == "waiting_approval" ||
                    e.Status == "waiting_delay"));
            if (hasRunning)
                throw new InvalidOperationException("Cannot archive a workflow with running, waiting-for-approval, or delayed executions. Cancel them first.");

            // Deactivate and soft-delete
            workflow.IsActive = false;
            workflow.IsDeleted = true;
            workflow.UpdatedAt = DateTime.UtcNow;
            workflow.ModifiedBy = modifiedBy;

            // Deactivate all triggers
            if (workflow.Triggers != null)
            {
                foreach (var trigger in workflow.Triggers)
                {
                    trigger.IsActive = false;
                }
            }

            await _db.SaveChangesAsync();
            return true;
        }


        public async Task<IEnumerable<WorkflowTriggerDto>> GetWorkflowTriggersAsync(int workflowId)
        {
            var triggers = await _db.WorkflowTriggers
                .Where(t => t.WorkflowId == workflowId)
                .ToListAsync();

            return triggers.Select(t => new WorkflowTriggerDto
            {
                Id = t.Id,
                WorkflowId = t.WorkflowId,
                NodeId = t.NodeId,
                EntityType = t.EntityType,
                FromStatus = t.FromStatus,
                ToStatus = t.ToStatus,
                IsActive = t.IsActive
            });
        }

        public async Task<WorkflowTriggerDto> RegisterTriggerAsync(RegisterTriggerDto dto)
        {
            var trigger = new WorkflowTrigger
            {
                WorkflowId = dto.WorkflowId,
                NodeId = dto.NodeId,
                EntityType = dto.EntityType,
                FromStatus = string.IsNullOrEmpty(dto.FromStatus) || dto.FromStatus == "any" ? null : dto.FromStatus,
                ToStatus = string.IsNullOrEmpty(dto.ToStatus) || dto.ToStatus == "any" ? null : dto.ToStatus,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.WorkflowTriggers.Add(trigger);
            await _db.SaveChangesAsync();

            return new WorkflowTriggerDto
            {
                Id = trigger.Id,
                WorkflowId = trigger.WorkflowId,
                NodeId = trigger.NodeId,
                EntityType = trigger.EntityType,
                FromStatus = trigger.FromStatus,
                ToStatus = trigger.ToStatus,
                IsActive = trigger.IsActive
            };
        }

        public async Task<bool> RemoveTriggerAsync(int triggerId)
        {
            var trigger = await _db.WorkflowTriggers.FirstOrDefaultAsync(t => t.Id == triggerId);
            if (trigger == null) return false;

            _db.WorkflowTriggers.Remove(trigger);
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<IEnumerable<WorkflowExecutionDto>> GetWorkflowExecutionsAsync(int workflowId, int page = 1, int pageSize = 20)
        {
            var executions = await _db.WorkflowExecutions
                .Where(e => e.WorkflowId == workflowId)
                .Include(e => e.Workflow)
                .Include(e => e.Logs)
                .OrderByDescending(e => e.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return executions.Select(MapExecutionToDto);
        }

        public async Task<WorkflowExecutionDto?> GetExecutionByIdAsync(int executionId)
        {
            var execution = await _db.WorkflowExecutions
                .Include(e => e.Workflow)
                .Include(e => e.Logs)
                .FirstOrDefaultAsync(e => e.Id == executionId);

            return execution == null ? null : MapExecutionToDto(execution);
        }

        public async Task<bool> CancelExecutionAsync(int executionId)
        {
            var execution = await _db.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == executionId);
            if (execution == null || execution.Status == "completed" || execution.Status == "cancelled")
                return false;

            execution.Status = "cancelled";
            execution.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RetryExecutionAsync(int executionId)
        {
            var execution = await _db.WorkflowExecutions
                .Include(e => e.Logs)
                .FirstOrDefaultAsync(e => e.Id == executionId);
            if (execution == null || execution.Status != "failed")
                return false;

            execution.Status = "running";
            execution.Error = null;
            execution.CompletedAt = null;

            // Determine the node to restart from: the last failed node, or the first node
            var lastFailedLog = execution.Logs?
                .Where(l => l.Status == "failed")
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefault();

            var restartNodeId = lastFailedLog?.NodeId ?? execution.CurrentNodeId;

            if (string.IsNullOrEmpty(restartNodeId))
            {
                // If no node to restart from, fail gracefully
                execution.Status = "failed";
                execution.Error = "Cannot determine restart node";
                await _db.SaveChangesAsync();
                return false;
            }

            // Single save: status=running and CurrentNodeId set atomically so that
            // CleanupStuckExecutionsAsync never sees running+old-node between two saves.
            execution.CurrentNodeId = restartNodeId;
            await _db.SaveChangesAsync();

            // Actually re-run the graph from the restart node so the execution
            // doesn't stay stuck in "running" forever.
            var variables = new Dictionary<string, object?>
            {
                ["entityId"] = execution.TriggerEntityId,
                ["entityType"] = execution.TriggerEntityType,
                ["retry"] = true,
            };
            // BUG FIX: re-hydrate Variables from the persisted Context so retried
            // executions still have access to created_*_id values produced before
            // the failure. Without this, downstream status nodes target entity #0.
            if (!string.IsNullOrEmpty(execution.Context))
            {
                try
                {
                    var saved = JsonSerializer.Deserialize<Dictionary<string, object?>>(execution.Context);
                    if (saved != null)
                    {
                        foreach (var kv in saved)
                        {
                            if (!variables.ContainsKey(kv.Key))
                                variables[kv.Key] = kv.Value;
                        }
                    }
                }
                catch { /* best-effort hydrate */ }
            }
            await RunGraphAsync(execution, restartNodeId, variables);

            return true;
        }
        
        public async Task<int> CleanupStuckExecutionsAsync(int olderThanMinutes = 5)
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-olderThanMinutes);
            
            var stuckExecutions = await _db.WorkflowExecutions
                .Where(e => e.Status == "running" && e.StartedAt < cutoffTime)
                .ToListAsync();
            
            foreach (var execution in stuckExecutions)
            {
                execution.Status = "failed";
                execution.Error = $"Execution timed out after {olderThanMinutes} minutes (auto-cleanup)";
                execution.CompletedAt = DateTime.UtcNow;
            }
            
            await _db.SaveChangesAsync();
            return stuckExecutions.Count;
        }
        
        public async Task<WorkflowExecutionDto?> TriggerManualExecutionAsync(int workflowId, string entityType, int entityId, string? userId = null)
        {
            var workflow = await _db.WorkflowDefinitions
                .Include(w => w.Triggers)
                .FirstOrDefaultAsync(w => w.Id == workflowId && !w.IsDeleted);
            
            if (workflow == null) return null;
            
            // Find a trigger for this entity type (prefer one without status constraints)
            var trigger = workflow.Triggers?
                .Where(t => t.EntityType == entityType && t.IsActive)
                .OrderBy(t => t.FromStatus != null ? 1 : 0) // Prefer triggers without fromStatus constraint
                .FirstOrDefault();
            
            var startNodeId = trigger?.NodeId ?? "manual-trigger";
            
            // Create execution record
            var execution = new WorkflowExecution
            {
                WorkflowId = workflowId,
                TriggerEntityType = entityType,
                TriggerEntityId = entityId,
                Status = "running",
                CurrentNodeId = startNodeId,
                Context = JsonSerializer.Serialize(new
                {
                    entityType,
                    entityId,
                    triggeredAt = DateTime.UtcNow,
                    triggerSource = "manual",
                    userId
                }),
                StartedAt = DateTime.UtcNow,
                TriggeredBy = userId
            };
            
            _db.WorkflowExecutions.Add(execution);
            await _db.SaveChangesAsync();
            
            // Log the manual trigger
            var triggerLog = new WorkflowExecutionLog
            {
                ExecutionId = execution.Id,
                NodeId = startNodeId,
                NodeType = "manual-trigger",
                Status = "completed",
                Input = JsonSerializer.Serialize(new { source = "manual", entityType, entityId }),
                Output = JsonSerializer.Serialize(new { triggered = true, manual = true }),
                Timestamp = DateTime.UtcNow
            };
            
            _db.WorkflowExecutionLogs.Add(triggerLog);
            await _db.SaveChangesAsync();

            // Actually execute the workflow graph starting from the trigger node.
            // Without this the execution row would stay "running" forever until
            // CleanupStuckExecutionsAsync marks it failed.
            var variables = new Dictionary<string, object?>
            {
                ["entityId"] = entityId,
                ["entityType"] = entityType,
                ["triggerSource"] = "manual",
                ["userId"] = userId,
            };
            await RunGraphAsync(execution, startNodeId, variables);

            return MapExecutionToDto(execution);
        }

        private async Task ExtractAndRegisterTriggersAsync(WorkflowDefinition workflow)
        {
            try
            {
                var nodes = JsonSerializer.Deserialize<List<JsonElement>>(workflow.Nodes);
                if (nodes == null) return;

                foreach (var node in nodes)
                {
                    // CRITICAL: Read business type from data.type, NOT from the React Flow "type" field
                    // React Flow types are: "entityTrigger", "conditionNode", "n8nNode", "entityAction"
                    // Business types are: "offer-status-trigger", "sale-status-trigger", etc.
                    string? nodeType = null;
                    
                    // First try data.type (business type - this is what we need)
                    if (node.TryGetProperty("data", out var dataElement) && 
                        dataElement.TryGetProperty("type", out var dataTypeElement))
                    {
                        nodeType = dataTypeElement.GetString();
                    }
                    
                    // Fall back to top-level type (for seed data where type IS the business type)
                    if (string.IsNullOrEmpty(nodeType) && node.TryGetProperty("type", out var typeElement))
                    {
                        nodeType = typeElement.GetString();
                    }
                    
                    if (nodeType == null || !nodeType.Contains("status-trigger")) continue;

                    var entityType = GetEntityTypeFromNodeType(nodeType);
                    if (entityType == null) continue;

                    var nodeId = node.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
                    string? fromStatus = null;
                    string? toStatus = null;

                    if (node.TryGetProperty("data", out var dataEl))
                    {
                        if (dataEl.TryGetProperty("fromStatus", out var fromElement))
                        {
                            var from = fromElement.GetString();
                            if (!string.IsNullOrEmpty(from) && from != "any") fromStatus = from;
                        }
                        if (dataEl.TryGetProperty("toStatus", out var toElement))
                        {
                            var to = toElement.GetString();
                            if (!string.IsNullOrEmpty(to) && to != "any") toStatus = to;
                        }
                        // Also check config.fromStatus / config.toStatus (NodeConfigPanel saves there)
                        if (fromStatus == null && dataEl.TryGetProperty("config", out var configEl))
                        {
                            if (configEl.TryGetProperty("fromStatus", out var cfgFrom))
                            {
                                var from = cfgFrom.GetString();
                                if (!string.IsNullOrEmpty(from) && from != "any") fromStatus = from;
                            }
                            if (toStatus == null && configEl.TryGetProperty("toStatus", out var cfgTo))
                            {
                                var to = cfgTo.GetString();
                                if (!string.IsNullOrEmpty(to) && to != "any") toStatus = to;
                            }
                        }
                    }

                    await RegisterTriggerAsync(new RegisterTriggerDto
                    {
                        WorkflowId = workflow.Id,
                        NodeId = nodeId,
                        EntityType = entityType,
                        FromStatus = fromStatus,
                        ToStatus = toStatus
                    });
                }
            }
            catch
            {
                // Silently fail if nodes JSON is invalid
            }
        }

        private static string? GetEntityTypeFromNodeType(string nodeType)
        {
            // Check most specific first to avoid substring collisions
            if (nodeType.Contains("service-order") || nodeType.Contains("service_order")) return "service_order";
            if (nodeType.Contains("dispatch")) return "dispatch";
            if (nodeType.Contains("job")) return "job";
            if (nodeType.Contains("offer")) return "offer";
            if (nodeType.Contains("sale")) return "sale";
            if (nodeType.Contains("deal")) return "deal";
            return null;
        }

        /// <summary>
        /// Save-time graph validation. Guarantees that any workflow accepted by the
        /// engine can be executed:
        ///   - JSON is parseable
        ///   - At least one node exists
        ///   - Every node has a non-empty id (duplicates are rejected)
        ///   - Every edge references real source/target node ids
        ///   - At least one trigger node is present (otherwise the workflow can never fire)
        /// Throws <see cref="InvalidOperationException"/> with a human-readable message on failure.
        /// </summary>
        private static void ValidateGraphOrThrow(string nodesJson, string edgesJson)
        {
            List<JsonElement> nodes;
            List<JsonElement> edges;
            try
            {
                nodes = JsonSerializer.Deserialize<List<JsonElement>>(nodesJson) ?? new();
                edges = JsonSerializer.Deserialize<List<JsonElement>>(edgesJson) ?? new();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Workflow graph is not valid JSON: {ex.Message}");
            }

            if (nodes.Count == 0)
                throw new InvalidOperationException("Workflow must contain at least one node.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var hasTrigger = false;

            foreach (var node in nodes)
            {
                var id = node.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id))
                    throw new InvalidOperationException("Every node must have a non-empty 'id'.");
                if (!ids.Add(id))
                    throw new InvalidOperationException($"Duplicate node id '{id}'.");

                // Resolve the business type (data.type takes precedence, then top-level type)
                string? nodeType = null;
                if (node.TryGetProperty("data", out var dataEl) &&
                    dataEl.TryGetProperty("type", out var dataTypeEl))
                {
                    nodeType = dataTypeEl.GetString();
                }
                if (string.IsNullOrEmpty(nodeType) && node.TryGetProperty("type", out var topTypeEl))
                {
                    nodeType = topTypeEl.GetString();
                }

                if (!string.IsNullOrEmpty(nodeType) &&
                    (nodeType.Contains("trigger", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(nodeType, "entityTrigger", StringComparison.OrdinalIgnoreCase)))
                {
                    hasTrigger = true;
                }
            }

            if (!hasTrigger)
                throw new InvalidOperationException(
                    "Workflow must contain at least one trigger node — otherwise it can never fire.");

            foreach (var edge in edges)
            {
                var source = edge.TryGetProperty("source", out var sEl) ? sEl.GetString() : null;
                var target = edge.TryGetProperty("target", out var tEl) ? tEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                    throw new InvalidOperationException("Every edge must have a 'source' and a 'target'.");
                if (!ids.Contains(source))
                    throw new InvalidOperationException($"Edge references missing source node '{source}'.");
                if (!ids.Contains(target))
                    throw new InvalidOperationException($"Edge references missing target node '{target}'.");
                if (string.Equals(source, target, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Edge cannot connect node '{source}' to itself.");
            }
        }

        private static WorkflowDefinitionDto MapToDto(WorkflowDefinition workflow, int? executionsCount = null)
        {
            return new WorkflowDefinitionDto
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Description = workflow.Description,
                Nodes = JsonSerializer.Deserialize<object>(workflow.Nodes) ?? new List<object>(),
                Edges = JsonSerializer.Deserialize<object>(workflow.Edges) ?? new List<object>(),
                IsActive = workflow.IsActive,
                Version = workflow.Version,
                CreatedBy = workflow.CreatedBy,
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
                TriggersCount = workflow.Triggers?.Count ?? 0,
                ExecutionsCount = executionsCount ?? workflow.Executions?.Count ?? 0,
                Triggers = workflow.Triggers?.Select(t => new WorkflowTriggerDto
                {
                    Id = t.Id,
                    WorkflowId = t.WorkflowId,
                    NodeId = t.NodeId,
                    EntityType = t.EntityType,
                    FromStatus = t.FromStatus,
                    ToStatus = t.ToStatus,
                    IsActive = t.IsActive
                }).ToList() ?? new List<WorkflowTriggerDto>()
            };
        }

        private static WorkflowExecutionDto MapExecutionToDto(WorkflowExecution execution)
        {
            return new WorkflowExecutionDto
            {
                Id = execution.Id,
                WorkflowId = execution.WorkflowId,
                WorkflowName = execution.Workflow?.Name,
                TriggerEntityType = execution.TriggerEntityType,
                TriggerEntityId = execution.TriggerEntityId,
                Status = execution.Status,
                CurrentNodeId = execution.CurrentNodeId,
                Context = string.IsNullOrEmpty(execution.Context) ? null : JsonSerializer.Deserialize<object>(execution.Context),
                Error = execution.Error,
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                TriggeredBy = execution.TriggeredBy,
                Duration = execution.CompletedAt.HasValue 
                    ? (long)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds 
                    : null,
                Logs = execution.Logs?.Select(log => new WorkflowExecutionLogDto
                {
                    Id = log.Id,
                    NodeId = log.NodeId,
                    NodeType = log.NodeType,
                    Status = log.Status,
                    Input = string.IsNullOrEmpty(log.Input) ? null : JsonSerializer.Deserialize<object>(log.Input),
                    Output = string.IsNullOrEmpty(log.Output) ? null : JsonSerializer.Deserialize<object>(log.Output),
                    Error = log.Error,
                    Duration = log.Duration,
                    Timestamp = log.Timestamp
                }).ToList() ?? new List<WorkflowExecutionLogDto>()
            };
        }
    }
}
