using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Models;

namespace MyApi.Modules.Projects.Services
{
    public class TaskChecklistService : ITaskChecklistService
    {
        private readonly ApplicationDbContext _context;

        public TaskChecklistService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Checklist CRUD
        public async Task<TaskChecklistResponseDto> CreateChecklistAsync(CreateTaskChecklistDto dto, string createdBy)
        {
            var maxSortOrder = 0;
            if (dto.ProjectTaskId.HasValue)
            {
                maxSortOrder = await _context.TaskChecklists
                    .Where(c => c.ProjectTaskId == dto.ProjectTaskId)
                    .MaxAsync(c => (int?)c.SortOrder) ?? 0;
            }
            else if (dto.DailyTaskId.HasValue)
            {
                maxSortOrder = await _context.TaskChecklists
                    .Where(c => c.DailyTaskId == dto.DailyTaskId)
                    .MaxAsync(c => (int?)c.SortOrder) ?? 0;
            }

            var checklist = new TaskChecklist
            {
                ProjectTaskId = dto.ProjectTaskId,
                DailyTaskId = dto.DailyTaskId,
                Title = dto.Title,
                Description = dto.Description,
                IsExpanded = true,
                SortOrder = maxSortOrder + 1,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow
            };

            _context.TaskChecklists.Add(checklist);
            await _context.SaveChangesAsync();

            return MapToDto(checklist);
        }

        public async Task<TaskChecklistResponseDto?> GetChecklistByIdAsync(int id)
        {
            var checklist = await _context.TaskChecklists
                .Include(c => c.Items.OrderBy(i => i.SortOrder))
                .FirstOrDefaultAsync(c => c.Id == id);

            return checklist == null ? null : MapToDto(checklist);
        }

        public async Task<TaskChecklistResponseDto?> UpdateChecklistAsync(int id, UpdateTaskChecklistDto dto, string modifiedBy)
        {
            var checklist = await _context.TaskChecklists
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (checklist == null) return null;

            if (dto.Title != null) checklist.Title = dto.Title;
            if (dto.Description != null) checklist.Description = dto.Description;
            if (dto.IsExpanded.HasValue) checklist.IsExpanded = dto.IsExpanded.Value;
            if (dto.SortOrder.HasValue) checklist.SortOrder = dto.SortOrder.Value;

            checklist.ModifiedBy = modifiedBy;
            checklist.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToDto(checklist);
        }

        public async Task<bool> DeleteChecklistAsync(int id)
        {
            var checklist = await _context.TaskChecklists.FindAsync(id);
            if (checklist == null) return false;

            _context.TaskChecklists.Remove(checklist);
            await _context.SaveChangesAsync();

            return true;
        }

        // Query by task
        public async Task<List<TaskChecklistResponseDto>> GetChecklistsForProjectTaskAsync(int projectTaskId)
        {
            var checklists = await _context.TaskChecklists
                .Include(c => c.Items.OrderBy(i => i.SortOrder))
                .Where(c => c.ProjectTaskId == projectTaskId)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            return checklists.Select(MapToDto).ToList();
        }

        public async Task<List<TaskChecklistResponseDto>> GetChecklistsForDailyTaskAsync(int dailyTaskId)
        {
            var checklists = await _context.TaskChecklists
                .Include(c => c.Items.OrderBy(i => i.SortOrder))
                .Where(c => c.DailyTaskId == dailyTaskId)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            return checklists.Select(MapToDto).ToList();
        }

        // Checklist Items CRUD
        public async Task<TaskChecklistItemResponseDto> CreateChecklistItemAsync(CreateChecklistItemDto dto, string createdBy)
        {
            var maxSortOrder = await _context.TaskChecklistItems
                .Where(i => i.ChecklistId == dto.ChecklistId)
                .MaxAsync(i => (int?)i.SortOrder) ?? 0;

            var item = new TaskChecklistItem
            {
                ChecklistId = dto.ChecklistId,
                Title = dto.Title,
                SortOrder = dto.SortOrder ?? (maxSortOrder + 1),
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow
            };

            _context.TaskChecklistItems.Add(item);
            await _context.SaveChangesAsync();

            return MapItemToDto(item);
        }

        public async Task<TaskChecklistItemResponseDto?> UpdateChecklistItemAsync(int id, UpdateChecklistItemDto dto, string modifiedBy)
        {
            var item = await _context.TaskChecklistItems.FindAsync(id);
            if (item == null) return null;

            if (dto.Title != null) item.Title = dto.Title;
            if (dto.SortOrder.HasValue) item.SortOrder = dto.SortOrder.Value;
            if (dto.IsCompleted.HasValue)
            {
                item.IsCompleted = dto.IsCompleted.Value;
                if (dto.IsCompleted.Value && !item.CompletedAt.HasValue)
                {
                    item.CompletedAt = DateTime.UtcNow;
                }
                else if (!dto.IsCompleted.Value)
                {
                    item.CompletedAt = null;
                    item.CompletedById = null;
                    item.CompletedByName = null;
                }
            }

            item.ModifiedBy = modifiedBy;
            item.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapItemToDto(item);
        }

        public async Task<bool> DeleteChecklistItemAsync(int id)
        {
            var item = await _context.TaskChecklistItems.FindAsync(id);
            if (item == null) return false;

            _context.TaskChecklistItems.Remove(item);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<TaskChecklistItemResponseDto?> ToggleChecklistItemAsync(int id, int userId, string userName)
        {
            var item = await _context.TaskChecklistItems.FindAsync(id);
            if (item == null) return null;

            item.IsCompleted = !item.IsCompleted;

            if (item.IsCompleted)
            {
                item.CompletedAt = DateTime.UtcNow;
                item.CompletedById = userId;
                item.CompletedByName = userName;
            }
            else
            {
                item.CompletedAt = null;
                item.CompletedById = null;
                item.CompletedByName = null;
            }

            item.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapItemToDto(item);
        }

        // Bulk operations
        public async Task<List<TaskChecklistItemResponseDto>> BulkCreateChecklistItemsAsync(BulkCreateChecklistItemsDto dto, string createdBy)
        {
            var maxSortOrder = await _context.TaskChecklistItems
                .Where(i => i.ChecklistId == dto.ChecklistId)
                .MaxAsync(i => (int?)i.SortOrder) ?? 0;

            var items = new List<TaskChecklistItem>();
            var sortOrder = maxSortOrder;

            foreach (var title in dto.Titles)
            {
                sortOrder++;
                var item = new TaskChecklistItem
                {
                    ChecklistId = dto.ChecklistId,
                    Title = title,
                    SortOrder = sortOrder,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.UtcNow
                };
                items.Add(item);
            }

            _context.TaskChecklistItems.AddRange(items);
            await _context.SaveChangesAsync();

            return items.Select(MapItemToDto).ToList();
        }

        public async Task ReorderChecklistItemsAsync(ReorderChecklistItemsDto dto)
        {
            var items = await _context.TaskChecklistItems
                .Where(i => dto.ItemIds.Contains(i.Id))
                .ToListAsync();

            for (int i = 0; i < dto.ItemIds.Count; i++)
            {
                var item = items.FirstOrDefault(x => x.Id == dto.ItemIds[i]);
                if (item != null)
                {
                    item.SortOrder = i + 1;
                }
            }

            await _context.SaveChangesAsync();
        }

        // Convert item to task
        public async Task<int> ConvertItemToTaskAsync(int itemId, string createdBy)
        {
            var item = await _context.TaskChecklistItems
                .Include(i => i.Checklist)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null) throw new InvalidOperationException("Checklist item not found");

            // Get the parent task info
            var checklist = item.Checklist;
            int? projectId = null;

            if (checklist.ProjectTaskId.HasValue)
            {
                var parentTask = await _context.ProjectTasks.FindAsync(checklist.ProjectTaskId.Value);
                if (parentTask != null && parentTask.RelatedEntityType == "project" && parentTask.RelatedEntityId.HasValue)
                {
                    projectId = parentTask.RelatedEntityId.Value;
                }
            }

            if (checklist.ProjectTaskId.HasValue && projectId.HasValue)
            {
                // Create a new ProjectTask
                var newTask = new ProjectTask
                {
                    Title = item.Title,
                    Description = $"Created from checklist: {checklist.Title}",
                    RelatedEntityType = "project",
                    RelatedEntityId = projectId.Value,
                    Status = "open",
                    TaskType = "other",
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.UtcNow
                };

                // Stage both the creation and the item removal in one save so that
                // a failure cannot leave a new task without removing the checklist item.
                _context.ProjectTasks.Add(newTask);
                _context.TaskChecklistItems.Remove(item);
                await _context.SaveChangesAsync();

                return newTask.Id;
            }
            else if (checklist.DailyTaskId.HasValue)
            {
                // Create a new DailyTask
                var parentDailyTask = await _context.DailyTasks.FindAsync(checklist.DailyTaskId.Value);

                var newTask = new DailyTask
                {
                    Title = item.Title,
                    Description = $"Created from checklist: {checklist.Title}",
                    Status = "todo",
                    Priority = "medium",
                    AssignedUserId = parentDailyTask?.AssignedUserId,
                    DueDate = DateTime.UtcNow.AddDays(1),
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.UtcNow
                };

                // Same: stage both operations atomically.
                _context.DailyTasks.Add(newTask);
                _context.TaskChecklistItems.Remove(item);
                await _context.SaveChangesAsync();

                return newTask.Id;
            }

            throw new InvalidOperationException("Cannot determine task type for conversion");
        }

        // Mapping helpers
        private TaskChecklistResponseDto MapToDto(TaskChecklist checklist)
        {
            var items = checklist.Items?.OrderBy(i => i.SortOrder).ToList() ?? new List<TaskChecklistItem>();
            var completedCount = items.Count(i => i.IsCompleted);
            var totalCount = items.Count;

            return new TaskChecklistResponseDto
            {
                Id = checklist.Id,
                ProjectTaskId = checklist.ProjectTaskId,
                DailyTaskId = checklist.DailyTaskId,
                Title = checklist.Title,
                Description = checklist.Description,
                IsExpanded = checklist.IsExpanded,
                SortOrder = checklist.SortOrder,
                CreatedDate = checklist.CreatedDate,
                CreatedBy = checklist.CreatedBy,
                ModifiedDate = checklist.ModifiedDate,
                ModifiedBy = checklist.ModifiedBy,
                Items = items.Select(MapItemToDto).ToList(),
                CompletedCount = completedCount,
                TotalCount = totalCount,
                ProgressPercent = totalCount > 0 ? (int)Math.Round((double)completedCount / totalCount * 100) : 0
            };
        }

        private TaskChecklistItemResponseDto MapItemToDto(TaskChecklistItem item)
        {
            return new TaskChecklistItemResponseDto
            {
                Id = item.Id,
                ChecklistId = item.ChecklistId,
                Title = item.Title,
                IsCompleted = item.IsCompleted,
                CompletedAt = item.CompletedAt,
                CompletedById = item.CompletedById,
                CompletedByName = item.CompletedByName,
                SortOrder = item.SortOrder,
                CreatedDate = item.CreatedDate,
                CreatedBy = item.CreatedBy,
                ModifiedDate = item.ModifiedDate,
                ModifiedBy = item.ModifiedBy
            };
        }
    }
}
