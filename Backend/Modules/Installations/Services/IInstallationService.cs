using MyApi.Modules.Installations.DTOs;

namespace MyApi.Modules.Installations.Services
{
    public interface IInstallationService
    {
        Task<PaginatedInstallationResponse> GetInstallationsAsync(
            string? search = null,
            string? status = null,
            string? contactId = null,
            int page = 1,
            int pageSize = 20,
            string sortBy = "created_at",
            string sortOrder = "desc"
        );

        Task<InstallationDto?> GetInstallationByIdAsync(int id);
        Task<InstallationDto> CreateInstallationAsync(CreateInstallationDto createDto, string userId);
        Task<InstallationDto?> UpdateInstallationAsync(int id, UpdateInstallationDto updateDto, string userId);
        Task<bool> DeleteInstallationAsync(int id, string userId);
        Task<List<MaintenanceHistoryDto>> GetMaintenanceHistoryAsync(int installationId, int page = 1, int pageSize = 20);
        Task<MaintenanceHistoryDto> AddMaintenanceHistoryAsync(int installationId, CreateMaintenanceHistoryDto historyDto, string userId);
        
        /// <summary>
        /// High-performance bulk import supporting up to 10,000+ installations with batch processing.
        /// </summary>
        Task<BulkImportInstallationResultDto> BulkImportInstallationsAsync(BulkImportInstallationRequestDto importRequest, string userId);
    }
}
