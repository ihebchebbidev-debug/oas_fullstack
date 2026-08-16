using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MyApi.Data;
using MyApi.Modules.WorkflowEngine.DTOs;
using MyApi.Modules.WorkflowEngine.Models;

namespace MyApi.Modules.WorkflowEngine.Services
{
    public class WorkflowApprovalService : IWorkflowApprovalService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<WorkflowApprovalService> _logger;
        private readonly IWorkflowGraphExecutor _graphExecutor;

        public WorkflowApprovalService(
            ApplicationDbContext db,
            ILogger<WorkflowApprovalService> logger,
            IWorkflowGraphExecutor graphExecutor)
        {
            _db = db;
            _logger = logger;
            _graphExecutor = graphExecutor;
        }

        public async Task<IEnumerable<WorkflowApprovalDto>> GetPendingApprovalsAsync(string userId, string? role = null)
        {
            var query = _db.WorkflowApprovals
                .Include(a => a.Execution)
                    .ThenInclude(e => e!.Workflow)
                .Where(a => a.Status == "pending");

            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(a => a.ApproverRole == role);
            }

            var approvals = await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return approvals.Select(MapToDto);
        }

        public async Task<WorkflowApprovalDto?> GetApprovalByIdAsync(int approvalId)
        {
            var approval = await _db.WorkflowApprovals
                .Include(a => a.Execution)
                    .ThenInclude(e => e!.Workflow)
                .FirstOrDefaultAsync(a => a.Id == approvalId);

            return approval == null ? null : MapToDto(approval);
        }

        public async Task<bool> RespondToApprovalAsync(int approvalId, ApprovalResponseDto response, string userId, IEnumerable<string>? callerRoles = null)
        {
            var approval = await _db.WorkflowApprovals
                .Include(a => a.Execution)
                .FirstOrDefaultAsync(a => a.Id == approvalId);

            if (approval == null || approval.Status != "pending")
                return false;

            // SECURITY FIX (SEC-2): enforce ApproverRole. Previously any authenticated user
            // could approve/reject any pending request regardless of their role.
            if (!string.IsNullOrWhiteSpace(approval.ApproverRole))
            {
                var roles = callerRoles?.Where(r => !string.IsNullOrWhiteSpace(r))
                                       .Select(r => r.Trim())
                                       .ToList() ?? new List<string>();
                var allowed = roles.Any(r => string.Equals(r, approval.ApproverRole, StringComparison.OrdinalIgnoreCase))
                              || roles.Any(r => string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase));
                if (!allowed)
                {
                    _logger.LogWarning(
                        "[WORKFLOW-APPROVAL] User {UserId} attempted to respond to approval {ApprovalId} requiring role '{Role}' but only has [{Roles}]",
                        userId, approvalId, approval.ApproverRole, string.Join(",", roles));
                    throw new UnauthorizedAccessException(
                        $"This approval requires role '{approval.ApproverRole}'.");
                }
            }



            approval.Status = response.Approved ? "approved" : "rejected";
            approval.ResponseNote = response.Note;
            approval.ApprovedById = userId;
            approval.RespondedAt = DateTime.UtcNow;

            // For both approved and rejected, mark execution as running so
            // ResumeAfterNodeAsync can route it to the correct branch.
            if (approval.Execution != null)
            {
                approval.Execution.Status = "running";
                approval.Execution.WaitingNodeId = null;
            }

            await _db.SaveChangesAsync();

            // Resume the graph after the approval node for BOTH approved and rejected.
            // The graph executor will follow whichever outgoing edge matches the branch:
            //   approved  → edge labelled "approved" or "yes" / "true"
            //   rejected  → edge labelled "rejected" or "no"  / "false"
            // If no matching edge exists, the execution completes cleanly with no further nodes.
            if (approval.Execution != null && !string.IsNullOrEmpty(approval.NodeId))
            {
                try
                {
                    var execution = approval.Execution;

                    // Re-hydrate Variables from the persisted Context so entity IDs and
                    // outputs from nodes that ran before the pause are still available.
                    var variables = new Dictionary<string, object?>
                    {
                        ["entityType"]     = execution.TriggerEntityType,
                        ["entityId"]       = execution.TriggerEntityId,
                        ["approvalId"]     = approval.Id,
                        ["approvedBy"]     = userId,
                        ["approvalNote"]   = response.Note ?? string.Empty,
                        ["approval_result"] = response.Approved ? "approved" : "rejected",
                        ["approval_approved"] = response.Approved
                    };
                    if (!string.IsNullOrEmpty(execution.Context))
                    {
                        try
                        {
                            var saved = JsonSerializer.Deserialize<Dictionary<string, object?>>(execution.Context);
                            if (saved != null)
                                foreach (var kv in saved)
                                    if (!variables.ContainsKey(kv.Key))
                                        variables[kv.Key] = kv.Value;
                        }
                        catch { /* best-effort */ }
                    }

                    var context = new WorkflowExecutionContext
                    {
                        WorkflowId         = execution.WorkflowId,
                        ExecutionId        = execution.Id,
                        TriggerEntityType  = execution.TriggerEntityType,
                        TriggerEntityId    = execution.TriggerEntityId,
                        UserId             = userId,
                        Variables          = variables
                    };

                    // Inject the branch so GetNextNodes picks the right edge.
                    // Approval node uses SelectedBranch = "approved" | "rejected".
                    context.Variables["_approval_branch"] = response.Approved ? "approved" : "rejected";

                    var result = await _graphExecutor.ResumeAfterNodeAsync(
                        execution.WorkflowId,
                        execution.Id,
                        approval.NodeId,
                        context);

                    execution.Status = result.FinalStatus;
                    execution.Error  = result.Error;
                    if (result.FinalStatus is "completed" or "failed")
                        execution.CompletedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();

                    _logger.LogInformation(
                        "[WORKFLOW-APPROVAL] Resumed execution {ExecutionId} after {Decision} on approval {ApprovalId}: status={Status}, nodes={Count}",
                        execution.Id, response.Approved ? "approval" : "rejection", approvalId, result.FinalStatus, result.NodesExecuted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[WORKFLOW-APPROVAL] Failed to resume execution {ExecutionId} after approval {ApprovalId}",
                        approval.ExecutionId, approvalId);
                    if (approval.Execution != null)
                    {
                        approval.Execution.Status = "failed";
                        approval.Execution.Error  = $"Resume failed: {ex.Message}";
                        approval.Execution.CompletedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync();
                    }
                }
            }

            return true;
        }

        public async Task<WorkflowApprovalDto> CreateApprovalRequestAsync(
            int executionId,
            string nodeId,
            string title,
            string? message,
            string approverRole,
            int timeoutHours = 24)
        {
            var approval = new WorkflowApproval
            {
                ExecutionId = executionId,
                NodeId = nodeId,
                Title = title,
                Message = message,
                ApproverRole = approverRole,
                Status = "pending",
                TimeoutHours = timeoutHours,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(timeoutHours)
            };

            _db.WorkflowApprovals.Add(approval);

            // Update execution status to waiting
            var execution = await _db.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == executionId);
            if (execution != null)
            {
                execution.Status = "waiting_approval";
                execution.CurrentNodeId = nodeId;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Created approval request {ApprovalId} for execution {ExecutionId}, expires at {ExpiresAt}",
                approval.Id, executionId, approval.ExpiresAt);

            return MapToDto(approval);
        }

        public async Task<int> ExpireTimedOutApprovalsAsync()
        {
            var expiredApprovals = await _db.WorkflowApprovals
                .Include(a => a.Execution)
                .Where(a => a.Status == "pending" && a.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            foreach (var approval in expiredApprovals)
            {
                approval.Status = "expired";
                approval.RespondedAt = DateTime.UtcNow;

                if (approval.Execution != null)
                {
                    approval.Execution.Status = "failed";
                    approval.Execution.CompletedAt = DateTime.UtcNow;
                    approval.Execution.Error = $"Approval request expired after {approval.TimeoutHours} hours";
                }
            }

            await _db.SaveChangesAsync();

            if (expiredApprovals.Any())
            {
                _logger.LogInformation("Expired {Count} timed-out approval requests", expiredApprovals.Count);
            }

            return expiredApprovals.Count;
        }

        private static WorkflowApprovalDto MapToDto(WorkflowApproval approval)
        {
            return new WorkflowApprovalDto
            {
                Id = approval.Id,
                ExecutionId = approval.ExecutionId,
                NodeId = approval.NodeId,
                Title = approval.Title,
                Message = approval.Message,
                ApproverRole = approval.ApproverRole,
                ApprovedById = approval.ApprovedById,
                Status = approval.Status,
                ResponseNote = approval.ResponseNote,
                TimeoutHours = approval.TimeoutHours,
                CreatedAt = approval.CreatedAt,
                RespondedAt = approval.RespondedAt,
                ExpiresAt = approval.ExpiresAt,
                WorkflowName = approval.Execution?.Workflow?.Name,
                EntityType = approval.Execution?.TriggerEntityType,
                EntityId = approval.Execution?.TriggerEntityId
            };
        }
    }
}
