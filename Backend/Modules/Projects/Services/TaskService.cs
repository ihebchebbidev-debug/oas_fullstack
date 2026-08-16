using MyApi.Data;
using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Models;
using MyApi.Modules.Notifications.Services;
using Microsoft.EntityFrameworkCore;

namespace MyApi.Modules.Projects.Services
{
    public class TaskService : ITaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TaskService> _logger;
        private readonly INotificationService _notificationService;

        public TaskService(ApplicationDbContext context, ILogger<TaskService> logger, INotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _notificationService = notificationService;
        }

        #region Project Tasks

        public async Task<List<ProjectTaskResponseDto>> GetTasksByEntityAsync(string entityType, int entityId)
        {
            try
            {
                var tasks = await _context.ProjectTasks
                    .AsNoTracking()
                    .Include(t => t.AssignedUser)
                    .Where(t => t.RelatedEntityType == entityType && t.RelatedEntityId == entityId)
                    .OrderByDescending(t => t.CreatedDate)
                    .ToListAsync();

                return tasks.Select(MapToProjectTaskDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tasks for entity {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<ProjectTaskResponseDto?> GetProjectTaskByIdAsync(int id)
        {
            try
            {
                var task = await _context.ProjectTasks
                    .AsNoTracking()
                    .Include(t => t.AssignedUser)
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();

                return task != null ? MapToProjectTaskDto(task) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project task by id {TaskId}", id);
                throw;
            }
        }

        public async Task<ProjectTaskResponseDto> CreateProjectTaskAsync(CreateProjectTaskRequestDto createDto, string createdByUser)
        {
            try
            {
                var task = new ProjectTask
                {
                    Title = createDto.Title,
                    Description = createDto.Description,
                    TaskType = createDto.TaskType,
                    Status = createDto.Status,
                    RelatedEntityType = createDto.RelatedEntityType,
                    RelatedEntityId = createDto.RelatedEntityId,
                    DueDate = createDto.DueDate,
                    AssignedUserId = createDto.AssignedUserId,
                    CreatedBy = createdByUser,
                    CreatedDate = DateTime.UtcNow
                };

                await ApplyParentTenantIdAsync(task);

                _context.ProjectTasks.Add(task);
                await _context.SaveChangesAsync();

                // Activity log: a task targeting a project shows up on that project's timeline.
                if (string.Equals(task.RelatedEntityType, "project", StringComparison.OrdinalIgnoreCase) && task.RelatedEntityId.HasValue)
                {
                    _context.Set<ProjectActivity>().Add(new ProjectActivity
                    {
                        ProjectId = task.RelatedEntityId.Value,
                        ActionType = "task_added",
                        Description = $"Task '{task.Title}' added",
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = createdByUser,
                        RelatedEntityId = task.Id,
                        RelatedEntityType = "Task"
                    });
                    await _context.SaveChangesAsync();
                }

                // Reload task with related data
                var createdTask = await GetProjectTaskByIdAsync(task.Id);
                _logger.LogInformation("Project task created successfully with ID {TaskId}", task.Id);
                
                return createdTask!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project task");
                throw;
            }
        }

        public async Task<ProjectTaskResponseDto?> UpdateProjectTaskAsync(int id, UpdateProjectTaskRequestDto updateDto, string modifiedByUser)
        {
            try
            {
                var task = await _context.ProjectTasks
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();

                if (task == null)
                    return null;

                // Update fields if provided
                if (!string.IsNullOrEmpty(updateDto.Title))
                    task.Title = updateDto.Title;

                if (updateDto.Description != null)
                    task.Description = updateDto.Description;

                if (!string.IsNullOrEmpty(updateDto.TaskType))
                    task.TaskType = updateDto.TaskType;

                if (!string.IsNullOrEmpty(updateDto.Status))
                    task.Status = updateDto.Status;

                if (updateDto.RelatedEntityType != null)
                    task.RelatedEntityType = updateDto.RelatedEntityType;

                if (updateDto.RelatedEntityId.HasValue)
                    task.RelatedEntityId = updateDto.RelatedEntityId.Value;

                if (updateDto.DueDate.HasValue)
                    task.DueDate = updateDto.DueDate.Value;

                if (updateDto.AssignedUserId.HasValue)
                    task.AssignedUserId = updateDto.AssignedUserId.Value;

                task.ModifiedBy = modifiedByUser;
                task.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Activity log: reflect every task change on the owning project's timeline.
                if (string.Equals(task.RelatedEntityType, "project", StringComparison.OrdinalIgnoreCase)
                    && task.RelatedEntityId.HasValue)
                {
                    string actionType;
                    string description;
                    if (!string.IsNullOrEmpty(updateDto.Status)
                        && updateDto.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                    {
                        actionType = "task_completed";
                        description = $"Task '{task.Title}' completed";
                    }
                    else if (!string.IsNullOrEmpty(updateDto.Status))
                    {
                        actionType = "task_status_changed";
                        description = $"Task '{task.Title}' status changed to {updateDto.Status}";
                    }
                    else
                    {
                        actionType = "task_updated";
                        description = $"Task '{task.Title}' updated";
                    }

                    _context.Set<ProjectActivity>().Add(new ProjectActivity
                    {
                        ProjectId = task.RelatedEntityId.Value,
                        ActionType = actionType,
                        Description = description,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = modifiedByUser,
                        RelatedEntityId = task.Id,
                        RelatedEntityType = "Task"
                    });
                    await _context.SaveChangesAsync();
                }

                // Reload task with related data
                var updatedTask = await GetProjectTaskByIdAsync(id);
                _logger.LogInformation("Project task updated successfully with ID {TaskId}", id);
                
                return updatedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project task with ID {TaskId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteProjectTaskAsync(int id, string deletedByUser)
        {
            try
            {
                var task = await _context.ProjectTasks
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();

                if (task == null)
                    return false;

                // Activity log: record the deletion on the owning project's timeline
                // before the task row (and its FK) is gone.
                if (string.Equals(task.RelatedEntityType, "project", StringComparison.OrdinalIgnoreCase)
                    && task.RelatedEntityId.HasValue)
                {
                    _context.Set<ProjectActivity>().Add(new ProjectActivity
                    {
                        ProjectId = task.RelatedEntityId.Value,
                        ActionType = "task_deleted",
                        Description = $"Task '{task.Title}' deleted",
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = deletedByUser,
                        RelatedEntityType = "Task"
                    });
                }

                _context.ProjectTasks.Remove(task);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Project task deleted successfully with ID {TaskId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project task with ID {TaskId}", id);
                throw;
            }
        }

        #endregion

        #region Daily Tasks

        public async Task<List<DailyTaskResponseDto>> GetUserDailyTasksAsync(int userId)
        {
            try
            {
                var tasks = await _context.DailyTasks
                    .AsNoTracking()
                    .Include(t => t.AssignedUser)
                    .Where(t => t.AssignedUserId == userId)
                    .OrderBy(t => t.DueDate)
                    .ToListAsync();

                return tasks.Select(MapToDailyTaskDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily tasks for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<DailyTaskResponseDto>> GetDailyTasksByDateAsync(DateTime date)
        {
            try
            {
                var tasks = await _context.DailyTasks
                    .Include(t => t.AssignedUser)
                    .Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == date.Date)
                    .OrderBy(t => t.DueDate)
                    .ToListAsync();

                return tasks.Select(MapToDailyTaskDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily tasks for date {Date}", date);
                throw;
            }
        }

        public async Task<DailyTaskResponseDto?> GetDailyTaskByIdAsync(int id)
        {
            try
            {
                var task = await _context.DailyTasks
                    .Include(t => t.AssignedUser)
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();

                return task != null ? MapToDailyTaskDto(task) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily task by id {TaskId}", id);
                throw;
            }
        }

        public async Task<DailyTaskResponseDto> CreateDailyTaskAsync(CreateDailyTaskRequestDto createDto, string createdByUser)
        {
            try
            {
                var task = new DailyTask
                {
                    Title = createDto.Title,
                    Description = createDto.Description,
                    TaskType = createDto.TaskType,
                    Status = createDto.Status ?? "open",
                    RelatedEntityType = createDto.RelatedEntityType,
                    RelatedEntityId = createDto.RelatedEntityId,
                    DueDate = createDto.DueDate,
                    AssignedUserId = createDto.AssignedUserId,
                    CreatedBy = createdByUser,
                    CreatedDate = DateTime.UtcNow
                };

                _context.DailyTasks.Add(task);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Daily task created successfully with ID {TaskId}", task.Id);
                return MapToDailyTaskDto(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating daily task");
                throw;
            }
        }

        public async Task<DailyTaskResponseDto?> UpdateDailyTaskAsync(int id, UpdateDailyTaskRequestDto updateDto, string modifiedByUser)
        {
            try
            {
                var task = await _context.DailyTasks
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();

                if (task == null)
                    return null;

                // Update fields if provided
                if (!string.IsNullOrEmpty(updateDto.Title))
                    task.Title = updateDto.Title;

                if (updateDto.Description != null)
                    task.Description = updateDto.Description;

                if (!string.IsNullOrEmpty(updateDto.TaskType))
                    task.TaskType = updateDto.TaskType;

                if (!string.IsNullOrEmpty(updateDto.Status))
                    task.Status = updateDto.Status;

                if (updateDto.RelatedEntityType != null)
                    task.RelatedEntityType = updateDto.RelatedEntityType;

                if (updateDto.RelatedEntityId.HasValue)
                    task.RelatedEntityId = updateDto.RelatedEntityId.Value;

                if (updateDto.DueDate.HasValue)
                    task.DueDate = updateDto.DueDate.Value;

                if (updateDto.AssignedUserId.HasValue)
                    task.AssignedUserId = updateDto.AssignedUserId.Value;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Daily task updated successfully with ID {TaskId}", id);
                return MapToDailyTaskDto(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating daily task with ID {TaskId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteDailyTaskAsync(int id, string deletedByUser)
        {
            try
            {
                var task = await _context.DailyTasks
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();

                if (task == null)
                    return false;

                _context.DailyTasks.Remove(task);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Daily task deleted successfully with ID {TaskId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting daily task with ID {TaskId}", id);
                throw;
            }
        }

        public async Task<bool> CompleteDailyTaskAsync(int id, string completedByUser)
        {
            try
            {
                var task = await _context.DailyTasks
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();

                if (task == null)
                    return false;

                // Removed IsCompleted and CompletedDate
                task.Status = "completed";

                await _context.SaveChangesAsync();

                _logger.LogInformation("Daily task {TaskId} marked as completed", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing daily task {TaskId}", id);
                throw;
            }
        }

        #endregion

        #region Task Search and Filtering

        public async Task<TaskListResponseDto> SearchTasksAsync(TaskSearchRequestDto searchRequest)
        {
            try
            {
                searchRequest ??= new TaskSearchRequestDto();
                if (searchRequest.PageNumber <= 0) searchRequest.PageNumber = 1;
                if (searchRequest.PageSize <= 0) searchRequest.PageSize = 20;
                if (searchRequest.PageSize > 200) searchRequest.PageSize = 200;
                var projectTasksQuery = _context.ProjectTasks
                    .Include(t => t.AssignedUser)
                    .AsQueryable();

                // Apply filters for project tasks
                if (!string.IsNullOrEmpty(searchRequest.SearchTerm))
                {
                    var searchTerm = searchRequest.SearchTerm.ToLower();
                    projectTasksQuery = projectTasksQuery.Where(t => 
                        (t.Title ?? string.Empty).ToLower().Contains(searchTerm) ||
                        (t.Description != null && t.Description.ToLower().Contains(searchTerm)));
                }

                if (!string.IsNullOrEmpty(searchRequest.TaskType))
                    projectTasksQuery = projectTasksQuery.Where(t => t.TaskType == searchRequest.TaskType);

                if (!string.IsNullOrEmpty(searchRequest.Status))
                    projectTasksQuery = projectTasksQuery.Where(t => t.Status == searchRequest.Status);

                if (!string.IsNullOrEmpty(searchRequest.RelatedEntityType))
                    projectTasksQuery = projectTasksQuery.Where(t => t.RelatedEntityType == searchRequest.RelatedEntityType);

                if (searchRequest.RelatedEntityId.HasValue)
                    projectTasksQuery = projectTasksQuery.Where(t => t.RelatedEntityId == searchRequest.RelatedEntityId.Value);

                if (searchRequest.AssignedUserId.HasValue)
                    projectTasksQuery = projectTasksQuery.Where(t => t.AssignedUserId == searchRequest.AssignedUserId.Value);

                if (searchRequest.DueDateFrom.HasValue)
                    projectTasksQuery = projectTasksQuery.Where(t => t.DueDate >= searchRequest.DueDateFrom.Value);

                if (searchRequest.DueDateTo.HasValue)
                    projectTasksQuery = projectTasksQuery.Where(t => t.DueDate <= searchRequest.DueDateTo.Value);

                // Apply pagination
                var totalCount = await projectTasksQuery.CountAsync();
                var skip = (searchRequest.PageNumber - 1) * searchRequest.PageSize;

                var projectTasks = await projectTasksQuery
                    .OrderByDescending(t => t.CreatedDate)
                    .Skip(skip)
                    .Take(searchRequest.PageSize)
                    .ToListAsync();

                var projectTaskDtos = projectTasks.Select(MapToProjectTaskDto).ToList();

                return new TaskListResponseDto
                {
                    ProjectTasks = projectTaskDtos,
                    DailyTasks = new List<DailyTaskResponseDto>(),
                    TotalCount = totalCount,
                    PageSize = searchRequest.PageSize,
                    PageNumber = searchRequest.PageNumber,
                    HasNextPage = skip + searchRequest.PageSize < totalCount,
                    HasPreviousPage = searchRequest.PageNumber > 1
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tasks");
                throw;
            }
        }

        public async Task<List<ProjectTaskResponseDto>> GetTasksByAssigneeAsync(int assigneeId, string? entityType = null, int? entityId = null)
        {
            try
            {
                var query = _context.ProjectTasks
                    .Include(t => t.AssignedUser)
                    .Where(t => t.AssignedUserId == assigneeId);

                if (!string.IsNullOrEmpty(entityType))
                    query = query.Where(t => t.RelatedEntityType == entityType);

                if (entityId.HasValue)
                    query = query.Where(t => t.RelatedEntityId == entityId.Value);

                var tasks = await query.ToListAsync();
                return tasks.Select(MapToProjectTaskDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tasks by assignee {AssigneeId}", assigneeId);
                throw;
            }
        }

        public async Task<List<ProjectTaskResponseDto>> GetOverdueTasksAsync(string? entityType = null, int? entityId = null, int? assigneeId = null)
        {
            try
            {
                var query = _context.ProjectTasks
                    .Include(t => t.AssignedUser)
                    .Where(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);

                if (!string.IsNullOrEmpty(entityType))
                    query = query.Where(t => t.RelatedEntityType == entityType);

                if (entityId.HasValue)
                    query = query.Where(t => t.RelatedEntityId == entityId.Value);

                if (assigneeId.HasValue)
                    query = query.Where(t => t.AssignedUserId == assigneeId.Value);

                var tasks = await query.ToListAsync();
                return tasks.Select(MapToProjectTaskDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting overdue tasks");
                throw;
            }
        }

        #endregion

        #region Task Movement and Positioning

        public async Task<bool> MoveTaskStatusAsync(int taskId, MoveTaskRequestDto moveDto, string movedByUser)
        {
            try
            {
                var task = await _context.ProjectTasks
                    .Where(t => t.Id == taskId)
                    .FirstOrDefaultAsync();

                if (task == null)
                    return false;

                task.Status = moveDto.Status;
                task.ModifiedBy = movedByUser;
                task.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Task {TaskId} status moved successfully", taskId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving task {TaskId} status", taskId);
                throw;
            }
        }

        public async Task<bool> BulkMoveTaskStatusesAsync(BulkMoveTasksRequestDto bulkMoveDto, string movedByUser)
        {
            // Set-based update: one SELECT to load all target rows, in-memory patch,
            // one SaveChanges. Replaces the previous N+1 loop over MoveTaskStatusAsync.
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var ids = bulkMoveDto.Tasks.Select(t => t.Id).ToList();
                    var statusById = bulkMoveDto.Tasks.ToDictionary(t => t.Id, t => t.Status);

                    var tasks = await _context.ProjectTasks
                        .Where(t => ids.Contains(t.Id))
                        .ToListAsync();

                    var now = DateTime.UtcNow;
                    foreach (var task in tasks)
                    {
                        if (statusById.TryGetValue(task.Id, out var newStatus))
                        {
                            task.Status = newStatus;
                            task.ModifiedBy = movedByUser;
                            task.ModifiedDate = now;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    _logger.LogInformation("Bulk moved status for {Count} tasks", tasks.Count);
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error during bulk task status move");
                    throw;
                }
            });
        }

        #endregion

        #region Task Assignment

        public async Task<bool> AssignTaskAsync(int taskId, AssignTaskRequestDto assignDto, string assignedByUser)
        {
            try
            {
                var task = await _context.ProjectTasks
                    .Where(t => t.Id == taskId)
                    .FirstOrDefaultAsync();

                if (task == null)
                    return false;

                var previousAssigneeId = task.AssignedUserId;
                task.AssignedUserId = assignDto.AssignedUserId;
                task.ModifiedBy = assignedByUser;
                task.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Send notification if assigning to a different user (not self-assignment)
                if (assignDto.AssignedUserId.HasValue &&
                    assignDto.AssignedUserId.Value > 0 &&
                    assignDto.AssignedUserId != previousAssigneeId)
                {
                    try
                    {
                        int fallbackEntityId = task.RelatedEntityId ?? 0;
                        await _notificationService.GenerateTaskAssignedNotificationAsync(
                            taskId,
                            task.Title,
                            assignDto.AssignedUserId.Value,
                            assignedByUser,
                            fallbackEntityId
                        );
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogWarning(notifyEx, "Failed to send task assignment notification for task {TaskId}", taskId);
                        // Don't fail the assignment if notification fails
                    }
                }

                _logger.LogInformation("Task {TaskId} assigned to user {AssignedUserId}", taskId, assignDto.AssignedUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning task {TaskId}", taskId);
                throw;
            }
        }

        public async Task<bool> UnassignTaskAsync(int taskId, string unassignedByUser)
        {
            try
            {
                var task = await _context.ProjectTasks
                    .Where(t => t.Id == taskId)
                    .FirstOrDefaultAsync();

                if (task == null)
                    return false;

                task.AssignedUserId = null;
                task.ModifiedBy = unassignedByUser;
                task.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Task {TaskId} unassigned", taskId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning task {TaskId}", taskId);
                throw;
            }
        }

        public async Task<bool> BulkAssignTasksAsync(BulkAssignTasksRequestDto bulkAssignDto, string assignedByUser)
        {
            try
            {
                var tasks = await _context.ProjectTasks
                    .Where(t => bulkAssignDto.TaskIds.Contains(t.Id))
                    .ToListAsync();

                foreach (var task in tasks)
                {
                    task.AssignedUserId = bulkAssignDto.AssignedUserId;
                    task.ModifiedBy = assignedByUser;
                    task.ModifiedDate = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Bulk assigned {Count} tasks to user {AssignedUserId}", tasks.Count, bulkAssignDto.AssignedUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk task assignment");
                throw;
            }
        }

        #endregion

        #region Task Statistics

        public async Task<TaskStatisticsDto> GetTaskStatisticsAsync(string? entityType = null, int? entityId = null)
        {
            try
            {
                var query = _context.ProjectTasks.AsQueryable();

                if (!string.IsNullOrEmpty(entityType))
                    query = query.Where(t => t.RelatedEntityType == entityType);
                
                if (entityId.HasValue)
                    query = query.Where(t => t.RelatedEntityId == entityId.Value);

                var totalTasks = await query.CountAsync();
                var overdueTasks = await query.CountAsync(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);

                var statusCounts = await query
                    .GroupBy(t => t.Status ?? "open")
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status, x => x.Count);

                var typeCounts = await query
                    .GroupBy(t => t.TaskType ?? "follow-up")
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Type, x => x.Count);

                return new TaskStatisticsDto
                {
                    TotalTasks = totalTasks,
                    OverdueTasks = overdueTasks,
                    TasksByStatus = statusCounts,
                    TasksByType = typeCounts
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting task statistics");
                throw;
            }
        }

        public async Task<Dictionary<int, ProjectTaskStatsDto>> GetBulkProjectTaskStatsAsync(List<int> projectIds)
        {
            var result = new Dictionary<int, ProjectTaskStatsDto>();
            if (projectIds == null || projectIds.Count == 0) return result;

            // One SQL round-trip: group tasks by project + status server-side.
            var now = DateTime.UtcNow;
            var rows = await _context.ProjectTasks
                .Where(t => t.RelatedEntityType == "project"
                    && t.RelatedEntityId.HasValue
                    && projectIds.Contains(t.RelatedEntityId.Value))
                .GroupBy(t => new { ProjectId = t.RelatedEntityId!.Value, Status = t.Status ?? "open" })
                .Select(g => new
                {
                    g.Key.ProjectId,
                    g.Key.Status,
                    Count = g.Count(),
                    Overdue = g.Count(t => t.DueDate.HasValue && t.DueDate.Value < now && (t.Status ?? "open") != "completed" && (t.Status ?? "open") != "done"),
                })
                .ToListAsync();

            foreach (var pid in projectIds)
            {
                var pidRows = rows.Where(r => r.ProjectId == pid).ToList();
                var total = pidRows.Sum(r => r.Count);
                var completed = pidRows.Where(r => r.Status == "completed" || r.Status == "done").Sum(r => r.Count);
                var overdue = pidRows.Sum(r => r.Overdue);
                result[pid] = new ProjectTaskStatsDto
                {
                    TotalTasks = total,
                    CompletedTasks = completed,
                    OverdueTasks = overdue,
                    CompletionPercentage = total > 0 ? Math.Round((double)completed / total * 100d, 2) : 0d,
                };
            }
            return result;
        }

        public async Task<bool> TaskExistsAsync(int id, bool isProjectTask = true)
        {
            if (isProjectTask)
                return await _context.ProjectTasks.AnyAsync(t => t.Id == id);
            else
                return await _context.DailyTasks.AnyAsync(t => t.Id == id);
        }

        #endregion

        #region Bulk Operations

        public async Task<bool> BulkUpdateTaskStatusAsync(BulkUpdateTaskStatusDto bulkUpdateDto, string updatedByUser)
        {
            try
            {
                if (bulkUpdateDto.TaskIds == null || !bulkUpdateDto.TaskIds.Any())
                    return false;

                var tasks = await _context.ProjectTasks
                    .Where(t => bulkUpdateDto.TaskIds.Contains(t.Id))
                    .ToListAsync();

                if (!tasks.Any())
                    return false;

                var now = DateTime.UtcNow;
                foreach (var task in tasks)
                {
                    task.Status = bulkUpdateDto.Status;
                    task.ModifiedDate = now;
                    task.ModifiedBy = updatedByUser;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating task status");
                throw;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Inherit TenantId from the parent project so per_company query filters return the task
        /// when the user views that project (fixes tasks saved with TenantId=0).
        /// </summary>
        private async Task ApplyParentTenantIdAsync(ProjectTask task)
        {
            if (!string.Equals(task.RelatedEntityType, "project", StringComparison.OrdinalIgnoreCase)
                || !task.RelatedEntityId.HasValue)
            {
                return;
            }

            var parentTenantId = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == task.RelatedEntityId.Value)
                .Select(p => (int?)p.TenantId)
                .FirstOrDefaultAsync();

            if (parentTenantId.HasValue)
            {
                task.TenantId = parentTenantId.Value;
            }
        }

        #endregion

        #region Private Mapping Methods

        private ProjectTaskResponseDto MapToProjectTaskDto(ProjectTask task)
        {
            // Get assigned user name - check both Users table and MainAdminUsers table
            string? assignedUserName = null;
            string? assignedUserProfilePictureUrl = null;
            
            if (task.AssignedUserId.HasValue)
            {
                // First try the navigation property (Users table)
                if (task.AssignedUser != null)
                {
                    assignedUserName = $"{task.AssignedUser.FirstName} {task.AssignedUser.LastName}".Trim();
                    assignedUserProfilePictureUrl = task.AssignedUser.ProfilePictureUrl;
                }
                else
                {
                    // Fallback: check MainAdminUsers table for the assigned user
                    var mainAdmin = _context.MainAdminUsers
                        .Where(u => u.Id == task.AssignedUserId.Value)
                        .Select(u => new { u.FirstName, u.LastName, u.ProfilePictureUrl })
                        .FirstOrDefault();
                    
                    if (mainAdmin != null)
                    {
                        assignedUserName = $"{mainAdmin.FirstName} {mainAdmin.LastName}".Trim();
                        assignedUserProfilePictureUrl = mainAdmin.ProfilePictureUrl;
                    }
                }
            }

            return new ProjectTaskResponseDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                TaskType = task.TaskType,
                Status = task.Status,
                RelatedEntityType = task.RelatedEntityType,
                RelatedEntityId = task.RelatedEntityId,
                DueDate = task.DueDate,
                AssignedUserId = task.AssignedUserId,
                AssignedUserName = assignedUserName,
                AssignedUserProfilePictureUrl = assignedUserProfilePictureUrl,
                CreatedDate = task.CreatedDate,
                CreatedBy = task.CreatedBy,
                ModifiedDate = task.ModifiedDate,
                ModifiedBy = task.ModifiedBy
            };
        }

        private DailyTaskResponseDto MapToDailyTaskDto(DailyTask task)
        {
            // Get assigned user name - check both Users table and MainAdminUsers table
            string? assignedUserName = null;
            string? assignedUserProfilePictureUrl = null;
            
            if (task.AssignedUserId.HasValue)
            {
                // First try the navigation property (Users table)
                if (task.AssignedUser != null)
                {
                    assignedUserName = $"{task.AssignedUser.FirstName} {task.AssignedUser.LastName}".Trim();
                    assignedUserProfilePictureUrl = task.AssignedUser.ProfilePictureUrl;
                }
                else
                {
                    // Fallback: check MainAdminUsers table for the assigned user
                    var mainAdmin = _context.MainAdminUsers
                        .Where(u => u.Id == task.AssignedUserId.Value)
                        .Select(u => new { u.FirstName, u.LastName, u.ProfilePictureUrl })
                        .FirstOrDefault();
                    
                    if (mainAdmin != null)
                    {
                        assignedUserName = $"{mainAdmin.FirstName} {mainAdmin.LastName}".Trim();
                        assignedUserProfilePictureUrl = mainAdmin.ProfilePictureUrl;
                    }
                }
            }

            return new DailyTaskResponseDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                TaskType = task.TaskType,
                Status = task.Status,
                RelatedEntityType = task.RelatedEntityType,
                RelatedEntityId = task.RelatedEntityId,
                DueDate = task.DueDate,
                AssignedUserId = task.AssignedUserId,
                AssignedUserName = assignedUserName,
                AssignedUserProfilePictureUrl = assignedUserProfilePictureUrl,
                CreatedDate = task.CreatedDate,
                CreatedBy = task.CreatedBy
            };
        }

        #endregion
    }
}
