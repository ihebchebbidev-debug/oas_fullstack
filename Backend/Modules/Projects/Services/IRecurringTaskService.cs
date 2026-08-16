using MyApi.Modules.Projects.DTOs;

namespace MyApi.Modules.Projects.Services
{
    public interface IRecurringTaskService
    {
        // CRUD
        Task<RecurringTaskResponseDto> CreateRecurringTaskAsync(CreateRecurringTaskDto dto, string createdBy);
        Task<RecurringTaskResponseDto?> GetRecurringTaskByIdAsync(int id);
        Task<RecurringTaskResponseDto?> UpdateRecurringTaskAsync(int id, UpdateRecurringTaskDto dto, string modifiedBy);
        Task<bool> DeleteRecurringTaskAsync(int id);

        // Query
        Task<List<RecurringTaskResponseDto>> GetRecurringTasksForProjectTaskAsync(int projectTaskId);
        Task<List<RecurringTaskResponseDto>> GetRecurringTasksForDailyTaskAsync(int dailyTaskId);
        Task<List<RecurringTaskResponseDto>> GetAllActiveRecurringTasksAsync();
        Task<List<RecurringTaskLogResponseDto>> GetLogsForRecurringTaskAsync(int recurringTaskId, int limit = 50);

        // Actions
        Task<RecurringTaskResponseDto?> PauseRecurringTaskAsync(int id, string modifiedBy);
        Task<RecurringTaskResponseDto?> ResumeRecurringTaskAsync(int id, string modifiedBy);

        // Generation
        Task<GenerateResultDto> GenerateDueTasksAsync(DateTime? upToDate = null, bool dryRun = false);
        Task<DateTime?> CalculateNextOccurrenceAsync(int recurringTaskId);
    }
}
