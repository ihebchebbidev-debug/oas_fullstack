using MyApi.Data;
using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyApi.Modules.Projects.Services
{
    public class TaskCommentService : ITaskCommentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TaskCommentService> _logger;

        public TaskCommentService(ApplicationDbContext context, ILogger<TaskCommentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<TaskCommentListResponseDto> GetTaskCommentsAsync(int? taskId = null, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                var query = _context.TaskComments.AsQueryable();

                if (taskId.HasValue)
                    query = query.Where(c => c.TaskId == taskId.Value);

                var totalCount = await query.CountAsync();
                var skip = (pageNumber - 1) * pageSize;

                var comments = await query
                    .OrderByDescending(c => c.CreatedDate)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                var commentDtos = comments.Select(MapToCommentDto).ToList();

                return new TaskCommentListResponseDto
                {
                    Comments = commentDtos,
                    TotalCount = totalCount,
                    PageSize = pageSize,
                    PageNumber = pageNumber,
                    HasNextPage = skip + pageSize < totalCount,
                    HasPreviousPage = pageNumber > 1
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting task comments");
                throw;
            }
        }

        public async Task<TaskCommentResponseDto?> GetCommentByIdAsync(int id)
        {
            try
            {
                var comment = await _context.TaskComments
                    .Where(c => c.Id == id)
                    .FirstOrDefaultAsync();

                return comment != null ? MapToCommentDto(comment) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comment by id {CommentId}", id);
                throw;
            }
        }

        public async Task<TaskCommentResponseDto> CreateCommentAsync(CreateTaskCommentRequestDto createDto, string createdByUser)
        {
            try
            {
                var comment = new TaskComment
                {
                    TaskId = createDto.TaskId,
                    Comment = createDto.Comment,
                    CreatedBy = createDto.CreatedBy ?? createdByUser,
                    CreatedDate = DateTime.UtcNow
                };

                _context.TaskComments.Add(comment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Comment created successfully with ID {CommentId}", comment.Id);
                return MapToCommentDto(comment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating comment");
                throw;
            }
        }

        public async Task<TaskCommentResponseDto?> UpdateCommentAsync(int id, UpdateTaskCommentRequestDto updateDto, string modifiedByUser)
        {
            try
            {
                var comment = await _context.TaskComments
                    .Where(c => c.Id == id)
                    .FirstOrDefaultAsync();

                if (comment == null)
                    return null;

                comment.Comment = updateDto.Comment;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Comment updated successfully with ID {CommentId}", id);
                return MapToCommentDto(comment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating comment with ID {CommentId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteCommentAsync(int id, string deletedByUser)
        {
            try
            {
                var comment = await _context.TaskComments
                    .Where(c => c.Id == id)
                    .FirstOrDefaultAsync();

                if (comment == null)
                    return false;

                _context.TaskComments.Remove(comment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Comment deleted successfully with ID {CommentId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment with ID {CommentId}", id);
                throw;
            }
        }

        public async Task<TaskCommentListResponseDto> SearchCommentsAsync(TaskCommentSearchRequestDto searchRequest)
        {
            return await GetTaskCommentsAsync(searchRequest.TaskId, searchRequest.PageNumber, searchRequest.PageSize);
        }

        public async Task<TaskCommentListResponseDto> GetCommentsByCreatorAsync(string createdBy, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                var query = _context.TaskComments
                    .Where(c => c.CreatedBy == createdBy);

                var totalCount = await query.CountAsync();
                var skip = (pageNumber - 1) * pageSize;

                var comments = await query
                    .OrderByDescending(c => c.CreatedDate)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                var commentDtos = comments.Select(MapToCommentDto).ToList();

                return new TaskCommentListResponseDto
                {
                    Comments = commentDtos,
                    TotalCount = totalCount,
                    PageSize = pageSize,
                    PageNumber = pageNumber,
                    HasNextPage = skip + pageSize < totalCount,
                    HasPreviousPage = pageNumber > 1
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comments by creator {CreatedBy}", createdBy);
                throw;
            }
        }

        public async Task<bool> CommentExistsAsync(int id)
        {
            return await _context.TaskComments.AnyAsync(c => c.Id == id);
        }

        public async Task<int> GetTaskCommentCountAsync(int taskId)
        {
            return await _context.TaskComments.CountAsync(c => c.TaskId == taskId);
        }

        public async Task<List<TaskCommentResponseDto>> GetMostRecentCommentsAsync(int count = 10)
        {
            var comments = await _context.TaskComments
                .OrderByDescending(c => c.CreatedDate)
                .Take(count)
                .ToListAsync();

            return comments.Select(MapToCommentDto).ToList();
        }

        public async Task<bool> BulkDeleteCommentsAsync(List<int> commentIds, string deletedByUser)
        {
            try
            {
                var comments = await _context.TaskComments
                    .Where(c => commentIds.Contains(c.Id))
                    .ToListAsync();

                _context.TaskComments.RemoveRange(comments);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Bulk deleted {Count} comments", comments.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk comment deletion");
                throw;
            }
        }

        public async Task<bool> DeleteAllTaskCommentsAsync(int taskId, string deletedByUser)
        {
            try
            {
                var comments = await _context.TaskComments
                    .Where(c => c.TaskId == taskId)
                    .ToListAsync();

                _context.TaskComments.RemoveRange(comments);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted all comments for task {TaskId}", taskId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all task comments for task {TaskId}", taskId);
                throw;
            }
        }

        private static TaskCommentResponseDto MapToCommentDto(TaskComment comment)
        {
            return new TaskCommentResponseDto
            {
                Id = comment.Id,
                TaskId = comment.TaskId,
                Comment = comment.Comment,
                CreatedDate = comment.CreatedDate,
                CreatedBy = comment.CreatedBy
            };
        }
    }
}
