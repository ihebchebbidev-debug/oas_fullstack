using MyApi.Modules.WorkflowEngine.DTOs;

namespace MyApi.Modules.WorkflowEngine.Services
{
    public interface IWorkflowEngineService
    {
        // Workflow Definitions
        Task<IEnumerable<WorkflowDefinitionDto>> GetAllWorkflowsAsync();
        Task<WorkflowDefinitionDto?> GetWorkflowByIdAsync(int id);
        Task<WorkflowDefinitionDto?> GetDefaultWorkflowAsync();
        Task<WorkflowDefinitionDto> CreateWorkflowAsync(CreateWorkflowDto dto, string createdBy);
        Task<WorkflowDefinitionDto?> UpdateWorkflowAsync(int id, UpdateWorkflowDto dto, string modifiedBy);
        Task<bool> DeleteWorkflowAsync(int id);
        Task<bool> ActivateWorkflowAsync(int id);
        Task<bool> DeactivateWorkflowAsync(int id);

        // Version Management
        Task<WorkflowDefinitionDto> CreateDraftFromWorkflowAsync(int id, string createdBy);
        Task<WorkflowDefinitionDto?> PromoteWorkflowAsync(int id, string modifiedBy);
        Task<bool> ArchiveWorkflowAsync(int id, string modifiedBy);

        // Workflow Triggers
        Task<IEnumerable<WorkflowTriggerDto>> GetWorkflowTriggersAsync(int workflowId);
        Task<WorkflowTriggerDto> RegisterTriggerAsync(RegisterTriggerDto dto);
        Task<bool> RemoveTriggerAsync(int triggerId);

        // Workflow Executions
        Task<IEnumerable<WorkflowExecutionDto>> GetWorkflowExecutionsAsync(int workflowId, int page = 1, int pageSize = 20);
        Task<WorkflowExecutionDto?> GetExecutionByIdAsync(int executionId);
        Task<bool> CancelExecutionAsync(int executionId);
        Task<bool> RetryExecutionAsync(int executionId);
        
        // Execution cleanup
        Task<int> CleanupStuckExecutionsAsync(int olderThanMinutes = 5);
        
        // Manual workflow execution
        Task<WorkflowExecutionDto?> TriggerManualExecutionAsync(int workflowId, string entityType, int entityId, string? userId = null);
    }
}
