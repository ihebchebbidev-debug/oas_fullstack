using MyApi.Modules.Shared.DTOs;

namespace MyApi.Modules.Shared.Services
{
    public interface ISystemLogService
    {
        // CRUD operations
        Task<SystemLogListResponseDto> GetLogsAsync(SystemLogSearchRequestDto? searchRequest = null);
        Task<SystemLogDto?> GetLogByIdAsync(int id);
        Task<SystemLogDto> CreateLogAsync(CreateSystemLogRequestDto createDto, string? ipAddress = null);
        
        // Statistics
        Task<SystemLogStatisticsDto> GetStatisticsAsync();
        
        // Utility methods
        Task<List<string>> GetModulesAsync();
        Task<CleanupResultDto> CleanupOldLogsAsync(int daysOld = 7);
        
        // Quick logging methods
        Task LogInfoAsync(string message, string module, string action = "other", string? userId = null, string? userName = null, string? entityType = null, string? entityId = null, string? details = null);
        Task LogWarningAsync(string message, string module, string action = "other", string? userId = null, string? userName = null, string? entityType = null, string? entityId = null, string? details = null);
        Task LogErrorAsync(string message, string module, string action = "other", string? userId = null, string? userName = null, string? entityType = null, string? entityId = null, string? details = null);
        Task LogSuccessAsync(string message, string module, string action = "other", string? userId = null, string? userName = null, string? entityType = null, string? entityId = null, string? details = null);
    }
}
