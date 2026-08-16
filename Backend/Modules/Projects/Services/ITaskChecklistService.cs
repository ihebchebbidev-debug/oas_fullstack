using MyApi.Modules.Projects.DTOs;

namespace MyApi.Modules.Projects.Services
{
    public interface ITaskChecklistService
    {
        // Checklist CRUD
        Task<TaskChecklistResponseDto> CreateChecklistAsync(CreateTaskChecklistDto dto, string createdBy);
        Task<TaskChecklistResponseDto?> GetChecklistByIdAsync(int id);
        Task<TaskChecklistResponseDto?> UpdateChecklistAsync(int id, UpdateTaskChecklistDto dto, string modifiedBy);
        Task<bool> DeleteChecklistAsync(int id);

        // Query by task
        Task<List<TaskChecklistResponseDto>> GetChecklistsForProjectTaskAsync(int projectTaskId);
        Task<List<TaskChecklistResponseDto>> GetChecklistsForDailyTaskAsync(int dailyTaskId);

        // Checklist Items CRUD
        Task<TaskChecklistItemResponseDto> CreateChecklistItemAsync(CreateChecklistItemDto dto, string createdBy);
        Task<TaskChecklistItemResponseDto?> UpdateChecklistItemAsync(int id, UpdateChecklistItemDto dto, string modifiedBy);
        Task<bool> DeleteChecklistItemAsync(int id);
        Task<TaskChecklistItemResponseDto?> ToggleChecklistItemAsync(int id, int userId, string userName);

        // Bulk operations
        Task<List<TaskChecklistItemResponseDto>> BulkCreateChecklistItemsAsync(BulkCreateChecklistItemsDto dto, string createdBy);
        Task ReorderChecklistItemsAsync(ReorderChecklistItemsDto dto);

        // Convert item to task
        Task<int> ConvertItemToTaskAsync(int itemId, string createdBy);
    }
}
