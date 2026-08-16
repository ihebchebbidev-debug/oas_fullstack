using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.WorkflowEngine.DTOs;
using MyApi.Modules.WorkflowEngine.Services;
using System.Security.Claims;

namespace MyApi.Modules.WorkflowEngine.Controllers
{
    [ApiController]
    [Route("api/workflow-executions")]
    [Authorize]
    public class WorkflowExecutionsController : ControllerBase
    {
        private readonly IWorkflowEngineService _workflowService;

        public WorkflowExecutionsController(IWorkflowEngineService workflowService)
        {
            _workflowService = workflowService;
        }

        /// <summary>
        /// Gets workflow executions for a specific workflow
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkflowExecutionDto>>> GetExecutions(
            [FromQuery] int workflowId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var executions = await _workflowService.GetWorkflowExecutionsAsync(workflowId, page, pageSize);
            return Ok(executions);
        }

        /// <summary>
        /// Gets a specific workflow execution by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<WorkflowExecutionDto>> GetById(int id)
        {
            var execution = await _workflowService.GetExecutionByIdAsync(id);
            if (execution == null)
                return NotFound(new { message = $"Execution {id} not found" });

            return Ok(execution);
        }

        /// <summary>
        /// Cancels a running workflow execution
        /// </summary>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var success = await _workflowService.CancelExecutionAsync(id);
            if (!success)
                return NotFound(new { message = $"Execution {id} not found or cannot be cancelled" });

            return Ok(new { message = "Execution cancelled" });
        }

        /// <summary>
        /// Retries a failed workflow execution
        /// </summary>
        [HttpPost("{id}/retry")]
        public async Task<IActionResult> Retry(int id)
        {
            var success = await _workflowService.RetryExecutionAsync(id);
            if (!success)
                return NotFound(new { message = $"Execution {id} not found or cannot be retried" });

            return Ok(new { message = "Execution restarted" });
        }
        
        /// <summary>
        /// Cleanup stuck executions (running longer than specified minutes)
        /// </summary>
        [HttpPost("cleanup-stuck")]
        public async Task<IActionResult> CleanupStuck([FromQuery] int olderThanMinutes = 5)
        {
            var count = await _workflowService.CleanupStuckExecutionsAsync(olderThanMinutes);
            return Ok(new { message = $"Cleaned up {count} stuck execution(s)", count });
        }
        
        /// <summary>
        /// Manually trigger a workflow execution for a specific entity
        /// </summary>
        [HttpPost("trigger-manual")]
        public async Task<IActionResult> TriggerManual([FromBody] ManualTriggerRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var execution = await _workflowService.TriggerManualExecutionAsync(
                request.WorkflowId, 
                request.EntityType, 
                request.EntityId, 
                userId);
            
            if (execution == null)
                return NotFound(new { message = $"Workflow {request.WorkflowId} not found" });
            
            return Ok(execution);
        }
    }
    
    public class ManualTriggerRequest
    {
        public int WorkflowId { get; set; }
        public string EntityType { get; set; } = "";
        public int EntityId { get; set; }
    }
}
