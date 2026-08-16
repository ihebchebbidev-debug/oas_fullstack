using MyApi.Modules.Projects.DTOs;

namespace MyApi.Modules.Projects.Services
{
    public interface ITaskCommentService
    {
        // Comment CRUD operations
        Task<TaskCommentListResponseDto> GetTaskCommentsAsync(int? taskId = null, int pageNumber = 1, int pageSize = 20);
        Task<TaskCommentResponseDto?> GetCommentByIdAsync(int id);
        Task<TaskCommentResponseDto> CreateCommentAsync(CreateTaskCommentRequestDto createDto, string createdByUser);
        Task<TaskCommentResponseDto?> UpdateCommentAsync(int id, UpdateTaskCommentRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteCommentAsync(int id, string deletedByUser);

        // Comment search and filtering
        Task<TaskCommentListResponseDto> SearchCommentsAsync(TaskCommentSearchRequestDto searchRequest);
        Task<TaskCommentListResponseDto> GetCommentsByCreatorAsync(string createdBy, int pageNumber = 1, int pageSize = 20);

        // Comment validation and utilities
        Task<bool> CommentExistsAsync(int id);
        Task<int> GetTaskCommentCountAsync(int taskId);

        // Recent comments
        Task<List<TaskCommentResponseDto>> GetMostRecentCommentsAsync(int count = 10);

        // Bulk operations
        Task<bool> BulkDeleteCommentsAsync(List<int> commentIds, string deletedByUser);
        Task<bool> DeleteAllTaskCommentsAsync(int taskId, string deletedByUser);
    }
}
