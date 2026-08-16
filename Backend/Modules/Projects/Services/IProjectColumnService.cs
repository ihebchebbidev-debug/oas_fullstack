using MyApi.Modules.Projects.DTOs;

namespace MyApi.Modules.Projects.Services
{
    public interface IProjectColumnService
    {
        // Column CRUD operations
        Task<ProjectColumnListResponseDto> GetProjectColumnsAsync(int projectId);
        Task<ProjectColumnResponseDto?> GetColumnByIdAsync(int id);
        Task<ProjectColumnResponseDto> CreateColumnAsync(CreateProjectColumnRequestDto createDto, string createdByUser);
        Task<ProjectColumnResponseDto?> UpdateColumnAsync(int id, UpdateProjectColumnRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteColumnAsync(int id, int? moveTasksToColumnId, string deletedByUser);
        
        // Column ordering
        Task<bool> ReorderColumnsAsync(int projectId, ReorderProjectColumnsRequestDto reorderDto, string updatedByUser);
        Task<int> GetNextColumnDisplayOrderAsync(int projectId);
        
        // Column validation and utilities
        Task<bool> ColumnExistsAsync(int id);
        Task<bool> ColumnBelongsToProjectAsync(int columnId, int projectId);
        Task<bool> UserCanManageProjectColumnsAsync(int projectId, int userId);
        Task<bool> CanDeleteColumnAsync(int columnId);
        Task<int> GetColumnTaskCountAsync(int columnId);
        
        // Default columns
        Task<bool> CreateDefaultColumnsAsync(int projectId, string createdByUser);
        Task<List<ProjectColumnResponseDto>> GetDefaultColumnTemplatesAsync();
        
        // Bulk operations
        Task<bool> BulkDeleteColumnsAsync(BulkDeleteProjectColumnsDto bulkDeleteDto, string deletedByUser);
        Task<bool> BulkUpdateColumnColorsAsync(Dictionary<int, string> columnColors, string updatedByUser);
    }
}
