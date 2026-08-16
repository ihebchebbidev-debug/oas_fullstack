using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.WorkflowEngine.DTOs;
using MyApi.Modules.WorkflowEngine.Services;
using System.Security.Claims;

namespace MyApi.Modules.WorkflowEngine.Controllers
{
    [ApiController]
    [Route("api/workflows")]
    [Authorize]
    public class WorkflowDefinitionsController : ControllerBase
    {
        private readonly IWorkflowEngineService _workflowService;

        public WorkflowDefinitionsController(IWorkflowEngineService workflowService)
        {
            _workflowService = workflowService;
        }

        /// <summary>
        /// Gets all workflow definitions
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkflowDefinitionDto>>> GetAll()
        {
            var workflows = await _workflowService.GetAllWorkflowsAsync();
            return Ok(workflows);
        }

        /// <summary>
        /// Gets the default active workflow (application-wide, always running)
        /// </summary>
        [HttpGet("default")]
        // SECURITY FIX: previously [AllowAnonymous]. Workflow graphs contain recipient lists,
        // endpoint URLs, and node-level credentials and must require authentication.
        public async Task<ActionResult<WorkflowDefinitionDto>> GetDefault()
        {
            var workflow = await _workflowService.GetDefaultWorkflowAsync();
            if (workflow == null)
                return NotFound(new { message = "Default workflow not found" });

            return Ok(workflow);
        }

        /// <summary>
        /// Gets a specific workflow definition by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<WorkflowDefinitionDto>> GetById(int id)
        {
            var workflow = await _workflowService.GetWorkflowByIdAsync(id);
            if (workflow == null)
                return NotFound(new { message = $"Workflow {id} not found" });

            return Ok(workflow);
        }

        /// <summary>
        /// Creates a new workflow definition
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<WorkflowDefinitionDto>> Create([FromBody] CreateWorkflowDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            try
            {
                var workflow = await _workflowService.CreateWorkflowAsync(dto, userId);
                return CreatedAtAction(nameof(GetById), new { id = workflow.Id }, workflow);
            }
            catch (InvalidOperationException ex)
            {
                // Save-time graph validation rejected the workflow
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing workflow definition
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<WorkflowDefinitionDto>> Update(int id, [FromBody] UpdateWorkflowDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            try
            {
                var workflow = await _workflowService.UpdateWorkflowAsync(id, dto, userId);
                if (workflow == null)
                    return NotFound(new { message = $"Workflow {id} not found" });
                return Ok(workflow);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Deletes a workflow definition
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _workflowService.DeleteWorkflowAsync(id);
            if (!success)
                return NotFound(new { message = $"Workflow {id} not found" });

            return NoContent();
        }

        /// <summary>
        /// Activates a workflow
        /// </summary>
        [HttpPost("{id}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            var success = await _workflowService.ActivateWorkflowAsync(id);
            if (!success)
                return NotFound(new { message = $"Workflow {id} not found" });

            return Ok(new { message = "Workflow activated" });
        }

        /// <summary>
        /// Deactivates a workflow
        /// </summary>
        [HttpPost("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(int id)
        {
            var success = await _workflowService.DeactivateWorkflowAsync(id);
            if (!success)
                return NotFound(new { message = $"Workflow {id} not found" });

            return Ok(new { message = "Workflow deactivated" });
        }

        /// <summary>
        /// Gets all triggers for a workflow
        /// </summary>
        [HttpGet("{id}/triggers")]
        public async Task<ActionResult<IEnumerable<WorkflowTriggerDto>>> GetTriggers(int id)
        {
            var triggers = await _workflowService.GetWorkflowTriggersAsync(id);
            return Ok(triggers);
        }

        /// <summary>
        /// Registers a new trigger for a workflow
        /// </summary>
        [HttpPost("{id}/triggers")]
        public async Task<ActionResult<WorkflowTriggerDto>> RegisterTrigger(int id, [FromBody] RegisterTriggerDto dto)
        {
            dto.WorkflowId = id;
            var trigger = await _workflowService.RegisterTriggerAsync(dto);
            return Created($"/api/workflows/{id}/triggers/{trigger.Id}", trigger);
        }

        /// <summary>
        /// Removes a trigger from a workflow
        /// </summary>
        [HttpDelete("{id}/triggers/{triggerId}")]
        public async Task<IActionResult> RemoveTrigger(int id, int triggerId)
        {
            var success = await _workflowService.RemoveTriggerAsync(triggerId);
            if (!success)
                return NotFound(new { message = $"Trigger {triggerId} not found" });

            return NoContent();
        }

        /// <summary>
        /// Creates a draft copy of an existing workflow for safe editing.
        /// The original workflow remains active and untouched.
        /// </summary>
        [HttpPost("{id}/create-draft")]
        public async Task<ActionResult<WorkflowDefinitionDto>> CreateDraft(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
                var draft = await _workflowService.CreateDraftFromWorkflowAsync(id, userId);
                return CreatedAtAction(nameof(GetById), new { id = draft.Id }, draft);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Promotes a draft workflow to active, deactivating any conflicting active version.
        /// </summary>
        [HttpPost("{id}/promote")]
        public async Task<ActionResult<WorkflowDefinitionDto>> Promote(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var workflow = await _workflowService.PromoteWorkflowAsync(id, userId);

            if (workflow == null)
                return NotFound(new { message = $"Workflow {id} not found" });

            return Ok(workflow);
        }

        /// <summary>
        /// Archives a workflow (soft-delete with history preservation).
        /// Cannot archive workflows with running executions.
        /// </summary>
        [HttpPost("{id}/archive")]
        public async Task<IActionResult> Archive(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
                var success = await _workflowService.ArchiveWorkflowAsync(id, userId);
                if (!success)
                    return NotFound(new { message = $"Workflow {id} not found" });

                return Ok(new { message = "Workflow archived" });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gets workflow executions for this workflow
        /// </summary>
        [HttpGet("{id}/executions")]
        public async Task<ActionResult<IEnumerable<WorkflowExecutionDto>>> GetExecutions(
            int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var executions = await _workflowService.GetWorkflowExecutionsAsync(id, page, pageSize);
            return Ok(executions);
        }
    }
}
