using MyApi.Modules.Projects.DTOs;

namespace MyApi.Modules.Projects.Services
{
    public interface ITaskTimeEntryService
    {
        // CRUD Operations
        Task<TaskTimeEntryResponseDto> CreateTimeEntryAsync(CreateTaskTimeEntryDto createDto, string createdByUser);
        Task<TaskTimeEntryResponseDto?> UpdateTimeEntryAsync(int id, UpdateTaskTimeEntryDto updateDto, string modifiedByUser);
        Task<bool> DeleteTimeEntryAsync(int id, string deletedByUser);
        Task<TaskTimeEntryResponseDto?> GetTimeEntryByIdAsync(int id);

        // Query Operations
        Task<List<TaskTimeEntryResponseDto>> GetTimeEntriesForProjectTaskAsync(int projectTaskId);
        Task<List<TaskTimeEntryResponseDto>> GetTimeEntriesForDailyTaskAsync(int dailyTaskId);
        Task<List<TaskTimeEntryResponseDto>> GetTimeEntriesByUserAsync(int userId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<TaskTimeEntryResponseDto>> GetTimeEntriesByProjectAsync(int projectId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<TaskTimeEntryResponseDto>> QueryTimeEntriesAsync(TaskTimeEntryQueryDto query);

        // Summary Operations
        Task<TaskTimeTrackingSummaryDto> GetProjectTaskTimeSummaryAsync(int projectTaskId);
        Task<TaskTimeTrackingSummaryDto> GetDailyTaskTimeSummaryAsync(int dailyTaskId);
        Task<decimal> GetTotalLoggedTimeForTaskAsync(int? projectTaskId, int? dailyTaskId);

        // Approval Operations
        Task<TaskTimeEntryResponseDto?> ApproveTimeEntryAsync(int id, ApproveTaskTimeEntryDto approveDto, int approverId);
        Task<bool> BulkApproveTimeEntriesAsync(BulkApproveTimeEntriesDto bulkDto, int approverId);

        // Timer Operations (for live time tracking)
        Task<TaskTimeEntryResponseDto> StartTimerAsync(int? projectTaskId, int? dailyTaskId, int userId, string? workType = "work");
        Task<TaskTimeEntryResponseDto?> StopTimerAsync(int timeEntryId, string? description = null);
        Task<TaskTimeEntryResponseDto?> GetActiveTimerAsync(int userId);
    }
}
