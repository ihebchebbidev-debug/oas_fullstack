using MyApi.Modules.Projects.DTOs;

namespace MyApi.Modules.Projects.Services
{
    public interface IProjectService
    {
        // Project CRUD operations
        Task<ProjectListResponseDto> GetAllProjectsAsync(ProjectSearchRequestDto? searchRequest = null);
        Task<ProjectResponseDto?> GetProjectByIdAsync(int id);
        Task<ProjectResponseDto> CreateProjectAsync(CreateProjectRequestDto createDto, string createdByUser);
        Task<ProjectResponseDto?> UpdateProjectAsync(int id, UpdateProjectRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteProjectAsync(int id, string deletedByUser);
        
        // Project statistics
        Task<ProjectStatisticsDto> GetStatisticsAsync();

        // Bulk operations
        Task<int> BulkUpdateStatusAsync(List<int> projectIds, string status, string userId);
        Task<int> BulkArchiveAsync(List<int> projectIds, bool archive, string userId);
        
        // Project search
        Task<List<ProjectResponseDto>> SearchProjectsAsync(string searchTerm);
        
        // Project Notes
        Task<List<ProjectNoteDto>> GetProjectNotesAsync(int projectId);
        Task<ProjectNoteDto> CreateProjectNoteAsync(int projectId, CreateProjectNoteRequestDto createDto, string createdByUser);
        Task<bool> DeleteProjectNoteAsync(int noteId, string deletedByUser);
        
        // Project Activity
        Task<List<ProjectActivityDto>> GetProjectActivityAsync(int projectId);
        Task<ProjectLinksDto> GetProjectLinksAsync(int projectId);
        Task<ProjectLinksDto> LinkEntityToProjectAsync(int projectId, LinkProjectEntityRequestDto dto, string userId);
        Task<ProjectLinksDto> UnlinkEntityFromProjectAsync(int projectId, string entityType, int entityId, string userId);
        Task<ProjectSettingsDto> GetProjectSettingsAsync();
        Task<ProjectSettingsDto> UpdateProjectSettingsAsync(ProjectSettingsDto dto, string userId);

        // Team members
        Task<List<int>> GetTeamMembersAsync(int projectId);
        Task<bool> AssignTeamMemberAsync(int projectId, AssignTeamMemberRequestDto dto, string userId);
        Task<bool> RemoveTeamMemberAsync(int projectId, int userIdToRemove, string userId);
    }
}
