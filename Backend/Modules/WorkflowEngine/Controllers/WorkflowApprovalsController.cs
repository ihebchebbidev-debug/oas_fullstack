using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.WorkflowEngine.DTOs;
using MyApi.Modules.WorkflowEngine.Services;
using System.Security.Claims;

namespace MyApi.Modules.WorkflowEngine.Controllers
{
    [ApiController]
    [Route("api/workflow-approvals")]
    [Authorize]
    public class WorkflowApprovalsController : ControllerBase
    {
        private readonly IWorkflowApprovalService _approvalService;

        public WorkflowApprovalsController(IWorkflowApprovalService approvalService)
        {
            _approvalService = approvalService;
        }

        /// <summary>
        /// Gets all pending approvals for the current user
        /// </summary>
        [HttpGet("pending")]
        public async Task<ActionResult<IEnumerable<WorkflowApprovalDto>>> GetPendingApprovals([FromQuery] string? role = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var approvals = await _approvalService.GetPendingApprovalsAsync(userId, role);
            return Ok(approvals);
        }

        /// <summary>
        /// Gets a specific approval request by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<WorkflowApprovalDto>> GetById(int id)
        {
            var approval = await _approvalService.GetApprovalByIdAsync(id);
            if (approval == null)
                return NotFound(new { message = $"Approval {id} not found" });

            return Ok(approval);
        }

        /// <summary>
        /// Approves a pending approval request
        /// </summary>
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> Approve(int id, [FromBody] ApprovalResponseDto? dto = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var response = dto ?? new ApprovalResponseDto { Approved = true };
            response.Approved = true;

            // SEC-2: pass caller roles so the service can enforce ApproverRole.
            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            try
            {
                var success = await _approvalService.RespondToApprovalAsync(id, response, userId, roles);
                if (!success)
                    return NotFound(new { message = $"Approval {id} not found or already processed" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }

            return Ok(new { message = "Request approved" });
        }

        /// <summary>
        /// Rejects a pending approval request
        /// </summary>
        [HttpPost("{id}/reject")]
        public async Task<IActionResult> Reject(int id, [FromBody] ApprovalResponseDto? dto = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var response = dto ?? new ApprovalResponseDto { Approved = false };
            response.Approved = false;

            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            try
            {
                var success = await _approvalService.RespondToApprovalAsync(id, response, userId, roles);
                if (!success)
                    return NotFound(new { message = $"Approval {id} not found or already processed" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }

            return Ok(new { message = "Request rejected" });
        }
    }
}
