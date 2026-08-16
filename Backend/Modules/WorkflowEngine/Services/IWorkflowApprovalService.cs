using MyApi.Modules.WorkflowEngine.DTOs;

namespace MyApi.Modules.WorkflowEngine.Services
{
    public interface IWorkflowApprovalService
    {
        /// <summary>
        /// Gets all pending approvals for a user based on their role.
        /// </summary>
        Task<IEnumerable<WorkflowApprovalDto>> GetPendingApprovalsAsync(string userId, string? role = null);

        /// <summary>
        /// Gets an approval request by ID.
        /// </summary>
        Task<WorkflowApprovalDto?> GetApprovalByIdAsync(int approvalId);

        /// <summary>
        /// Approves or rejects an approval request. <paramref name="callerRoles"/> is checked
        /// against the approval's required role (SEC-2 — role enforcement).
        /// </summary>
        Task<bool> RespondToApprovalAsync(int approvalId, ApprovalResponseDto response, string userId, IEnumerable<string>? callerRoles = null);

        /// <summary>
        /// Creates a new approval request for a workflow execution.
        /// </summary>
        Task<WorkflowApprovalDto> CreateApprovalRequestAsync(
            int executionId,
            string nodeId,
            string title,
            string? message,
            string approverRole,
            int timeoutHours = 24
        );

        /// <summary>
        /// Checks for and expires timed-out approval requests.
        /// </summary>
        Task<int> ExpireTimedOutApprovalsAsync();
    }
}
