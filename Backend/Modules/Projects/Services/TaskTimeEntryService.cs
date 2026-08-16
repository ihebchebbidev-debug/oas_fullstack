using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Models;

namespace MyApi.Modules.Projects.Services
{
    public class TaskTimeEntryService : ITaskTimeEntryService
    {
        private readonly ApplicationDbContext _context;

        public TaskTimeEntryService(ApplicationDbContext context)
        {
            _context = context;
        }

        // CRUD Operations
        public async Task<TaskTimeEntryResponseDto> CreateTimeEntryAsync(CreateTaskTimeEntryDto createDto, string createdByUser)
        {
            var entry = new TaskTimeEntry
            {
                ProjectTaskId = createDto.ProjectTaskId,
                DailyTaskId = createDto.DailyTaskId,
                UserId = createDto.UserId ?? 0,
                StartTime = createDto.StartTime,
                EndTime = createDto.EndTime,
                Duration = createDto.Duration ?? CalculateDuration(createDto.StartTime, createDto.EndTime),
                Description = createDto.Description,
                IsBillable = createDto.IsBillable,
                HourlyRate = createDto.HourlyRate,
                WorkType = createDto.WorkType ?? "work",
                CreatedBy = createdByUser,
                CreatedDate = DateTime.UtcNow
            };

            // Calculate total cost if hourly rate is provided
            if (entry.HourlyRate.HasValue && entry.Duration > 0)
            {
                entry.TotalCost = (entry.Duration / 60m) * entry.HourlyRate.Value;
            }

            // Get user name
            var user = await _context.Users.FindAsync(entry.UserId);
            if (user != null)
            {
                entry.UserName = $"{user.FirstName} {user.LastName}";
            }

            _context.TaskTimeEntries.Add(entry);
            await _context.SaveChangesAsync();

            return await MapToResponseDto(entry);
        }

        public async Task<TaskTimeEntryResponseDto?> UpdateTimeEntryAsync(int id, UpdateTaskTimeEntryDto updateDto, string modifiedByUser)
        {
            var entry = await _context.TaskTimeEntries.FindAsync(id);
            if (entry == null) return null;

            if (updateDto.StartTime.HasValue)
                entry.StartTime = updateDto.StartTime.Value;

            if (updateDto.EndTime.HasValue)
                entry.EndTime = updateDto.EndTime.Value;

            if (updateDto.Duration.HasValue)
                entry.Duration = updateDto.Duration.Value;
            else if (updateDto.StartTime.HasValue || updateDto.EndTime.HasValue)
                entry.Duration = CalculateDuration(entry.StartTime, entry.EndTime);

            if (updateDto.Description != null)
                entry.Description = updateDto.Description;

            if (updateDto.IsBillable.HasValue)
                entry.IsBillable = updateDto.IsBillable.Value;

            if (updateDto.HourlyRate.HasValue)
                entry.HourlyRate = updateDto.HourlyRate.Value;

            if (updateDto.WorkType != null)
                entry.WorkType = updateDto.WorkType;

            // Recalculate total cost
            if (entry.HourlyRate.HasValue && entry.Duration > 0)
            {
                entry.TotalCost = (entry.Duration / 60m) * entry.HourlyRate.Value;
            }

            entry.ModifiedBy = modifiedByUser;
            entry.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await MapToResponseDto(entry);
        }

        public async Task<bool> DeleteTimeEntryAsync(int id, string deletedByUser)
        {
            var entry = await _context.TaskTimeEntries.FindAsync(id);
            if (entry == null) return false;

            _context.TaskTimeEntries.Remove(entry);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<TaskTimeEntryResponseDto?> GetTimeEntryByIdAsync(int id)
        {
            var entry = await _context.TaskTimeEntries
                .Include(e => e.ProjectTask)
                .Include(e => e.DailyTask)
                .Include(e => e.User)
                .Include(e => e.ApprovedBy)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null) return null;

            return await MapToResponseDto(entry);
        }

        // Query Operations
        public async Task<List<TaskTimeEntryResponseDto>> GetTimeEntriesForProjectTaskAsync(int projectTaskId)
        {
            var entries = await _context.TaskTimeEntries
                .Where(e => e.ProjectTaskId == projectTaskId)
                .OrderByDescending(e => e.StartTime)
                .ToListAsync();

            var result = new List<TaskTimeEntryResponseDto>();
            foreach (var entry in entries)
            {
                result.Add(await MapToResponseDto(entry));
            }
            return result;
        }

        public async Task<List<TaskTimeEntryResponseDto>> GetTimeEntriesForDailyTaskAsync(int dailyTaskId)
        {
            var entries = await _context.TaskTimeEntries
                .Where(e => e.DailyTaskId == dailyTaskId)
                .OrderByDescending(e => e.StartTime)
                .ToListAsync();

            var result = new List<TaskTimeEntryResponseDto>();
            foreach (var entry in entries)
            {
                result.Add(await MapToResponseDto(entry));
            }
            return result;
        }

        public async Task<List<TaskTimeEntryResponseDto>> GetTimeEntriesByUserAsync(int userId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.TaskTimeEntries.Where(e => e.UserId == userId);

            if (fromDate.HasValue)
                query = query.Where(e => e.StartTime >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.StartTime <= toDate.Value);

            var entries = await query.OrderByDescending(e => e.StartTime).ToListAsync();

            var result = new List<TaskTimeEntryResponseDto>();
            foreach (var entry in entries)
            {
                result.Add(await MapToResponseDto(entry));
            }
            return result;
        }

        public async Task<List<TaskTimeEntryResponseDto>> GetTimeEntriesByProjectAsync(int projectId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.TaskTimeEntries
                .Where(e => e.ProjectTask != null && e.ProjectTask.RelatedEntityType == "project" && e.ProjectTask.RelatedEntityId == projectId);

            if (fromDate.HasValue)
                query = query.Where(e => e.StartTime >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.StartTime <= toDate.Value);

            var entries = await query.OrderByDescending(e => e.StartTime).ToListAsync();

            var result = new List<TaskTimeEntryResponseDto>();
            foreach (var entry in entries)
            {
                result.Add(await MapToResponseDto(entry));
            }
            return result;
        }

        public async Task<List<TaskTimeEntryResponseDto>> QueryTimeEntriesAsync(TaskTimeEntryQueryDto query)
        {
            var queryable = _context.TaskTimeEntries.AsQueryable();

            if (query.ProjectTaskId.HasValue)
                queryable = queryable.Where(e => e.ProjectTaskId == query.ProjectTaskId);

            if (query.DailyTaskId.HasValue)
                queryable = queryable.Where(e => e.DailyTaskId == query.DailyTaskId);

            if (query.UserId.HasValue)
                queryable = queryable.Where(e => e.UserId == query.UserId);

            if (query.ProjectId.HasValue)
                queryable = queryable.Include(e => e.ProjectTask)
                    .Where(e => e.ProjectTask != null && e.ProjectTask.RelatedEntityType == "project" && e.ProjectTask.RelatedEntityId == query.ProjectId);

            if (query.FromDate.HasValue)
                queryable = queryable.Where(e => e.StartTime >= query.FromDate.Value);

            if (query.ToDate.HasValue)
                queryable = queryable.Where(e => e.StartTime <= query.ToDate.Value);

            if (query.IsBillable.HasValue)
                queryable = queryable.Where(e => e.IsBillable == query.IsBillable.Value);

            if (!string.IsNullOrEmpty(query.ApprovalStatus))
                queryable = queryable.Where(e => e.ApprovalStatus == query.ApprovalStatus);

            if (!string.IsNullOrEmpty(query.WorkType))
                queryable = queryable.Where(e => e.WorkType == query.WorkType);

            // Sorting
            queryable = query.SortDirection.ToLower() == "asc"
                ? queryable.OrderBy(e => EF.Property<object>(e, query.SortBy))
                : queryable.OrderByDescending(e => EF.Property<object>(e, query.SortBy));

            // Pagination
            var entries = await queryable
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            var result = new List<TaskTimeEntryResponseDto>();
            foreach (var entry in entries)
            {
                result.Add(await MapToResponseDto(entry));
            }
            return result;
        }

        // Summary Operations
        public async Task<TaskTimeTrackingSummaryDto> GetProjectTaskTimeSummaryAsync(int projectTaskId)
        {
            var task = await _context.ProjectTasks.FindAsync(projectTaskId);
            var entries = await GetTimeEntriesForProjectTaskAsync(projectTaskId);

            var totalMinutes = entries.Sum(e => e.Duration);
            var billableMinutes = entries.Where(e => e.IsBillable).Sum(e => e.Duration);
            var totalCost = entries.Sum(e => e.TotalCost ?? 0);

            return new TaskTimeTrackingSummaryDto
            {
                TaskId = projectTaskId,
                TaskType = "project",
                TaskTitle = task?.Title ?? "",
                TotalLoggedMinutes = totalMinutes,
                EstimatedHours = null, // Add EstimatedHours to ProjectTask model if needed
                RemainingEstimate = null,
                TotalBillableMinutes = billableMinutes,
                TotalCost = totalCost,
                EntryCount = entries.Count,
                Entries = entries
            };
        }

        public async Task<TaskTimeTrackingSummaryDto> GetDailyTaskTimeSummaryAsync(int dailyTaskId)
        {
            var task = await _context.DailyTasks.FindAsync(dailyTaskId);
            var entries = await GetTimeEntriesForDailyTaskAsync(dailyTaskId);

            var totalMinutes = entries.Sum(e => e.Duration);
            var billableMinutes = entries.Where(e => e.IsBillable).Sum(e => e.Duration);
            var totalCost = entries.Sum(e => e.TotalCost ?? 0);

            return new TaskTimeTrackingSummaryDto
            {
                TaskId = dailyTaskId,
                TaskType = "daily",
                TaskTitle = task?.Title ?? "",
                TotalLoggedMinutes = totalMinutes,
                EstimatedHours = null,
                RemainingEstimate = null,
                TotalBillableMinutes = billableMinutes,
                TotalCost = totalCost,
                EntryCount = entries.Count,
                Entries = entries
            };
        }

        public async Task<decimal> GetTotalLoggedTimeForTaskAsync(int? projectTaskId, int? dailyTaskId)
        {
            var query = _context.TaskTimeEntries.AsQueryable();

            if (projectTaskId.HasValue)
                query = query.Where(e => e.ProjectTaskId == projectTaskId);

            if (dailyTaskId.HasValue)
                query = query.Where(e => e.DailyTaskId == dailyTaskId);

            return await query.SumAsync(e => e.Duration);
        }

        // Approval Operations
        public async Task<TaskTimeEntryResponseDto?> ApproveTimeEntryAsync(int id, ApproveTaskTimeEntryDto approveDto, int approverId)
        {
            var entry = await _context.TaskTimeEntries.FindAsync(id);
            if (entry == null) return null;

            entry.ApprovalStatus = approveDto.IsApproved ? "approved" : "rejected";
            entry.ApprovedById = approverId;
            entry.ApprovedDate = DateTime.UtcNow;
            entry.ApprovalNotes = approveDto.Notes;

            await _context.SaveChangesAsync();

            return await MapToResponseDto(entry);
        }

        public async Task<bool> BulkApproveTimeEntriesAsync(BulkApproveTimeEntriesDto bulkDto, int approverId)
        {
            var entries = await _context.TaskTimeEntries
                .Where(e => bulkDto.TimeEntryIds.Contains(e.Id))
                .ToListAsync();

            foreach (var entry in entries)
            {
                entry.ApprovalStatus = bulkDto.IsApproved ? "approved" : "rejected";
                entry.ApprovedById = approverId;
                entry.ApprovedDate = DateTime.UtcNow;
                entry.ApprovalNotes = bulkDto.Notes;
            }

            await _context.SaveChangesAsync();

            return true;
        }

        // Timer Operations
        public async Task<TaskTimeEntryResponseDto> StartTimerAsync(int? projectTaskId, int? dailyTaskId, int userId, string? workType = "work")
        {
            // Check if user already has an active timer
            var activeTimer = await _context.TaskTimeEntries
                .FirstOrDefaultAsync(e => e.UserId == userId && e.EndTime == null);

            if (activeTimer != null)
            {
                // Stop the existing timer first
                activeTimer.EndTime = DateTime.UtcNow;
                activeTimer.Duration = CalculateDuration(activeTimer.StartTime, activeTimer.EndTime);
            }

            var user = await _context.Users.FindAsync(userId);

            var entry = new TaskTimeEntry
            {
                ProjectTaskId = projectTaskId,
                DailyTaskId = dailyTaskId,
                UserId = userId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : null,
                StartTime = DateTime.UtcNow,
                EndTime = null,
                Duration = 0,
                WorkType = workType ?? "work",
                CreatedBy = user?.Email ?? "system",
                CreatedDate = DateTime.UtcNow
            };

            _context.TaskTimeEntries.Add(entry);
            await _context.SaveChangesAsync();

            return await MapToResponseDto(entry);
        }

        public async Task<TaskTimeEntryResponseDto?> StopTimerAsync(int timeEntryId, string? description = null)
        {
            var entry = await _context.TaskTimeEntries.FindAsync(timeEntryId);
            if (entry == null || entry.EndTime != null) return null;

            entry.EndTime = DateTime.UtcNow;
            entry.Duration = CalculateDuration(entry.StartTime, entry.EndTime);
            entry.Description = description;

            // Calculate total cost if hourly rate is set
            if (entry.HourlyRate.HasValue && entry.Duration > 0)
            {
                entry.TotalCost = (entry.Duration / 60m) * entry.HourlyRate.Value;
            }

            await _context.SaveChangesAsync();

            return await MapToResponseDto(entry);
        }

        public async Task<TaskTimeEntryResponseDto?> GetActiveTimerAsync(int userId)
        {
            var entry = await _context.TaskTimeEntries
                .Include(e => e.ProjectTask)
                .Include(e => e.DailyTask)
                .FirstOrDefaultAsync(e => e.UserId == userId && e.EndTime == null);

            if (entry == null) return null;

            return await MapToResponseDto(entry);
        }

        // Helper Methods
        private decimal CalculateDuration(DateTime startTime, DateTime? endTime)
        {
            if (!endTime.HasValue) return 0;
            return (decimal)(endTime.Value - startTime).TotalMinutes;
        }

        private async Task<TaskTimeEntryResponseDto> MapToResponseDto(TaskTimeEntry entry)
        {
            string? projectTaskTitle = null;
            string? dailyTaskTitle = null;
            string? approvedByName = null;

            if (entry.ProjectTaskId.HasValue)
            {
                var projectTask = entry.ProjectTask ?? await _context.ProjectTasks.FindAsync(entry.ProjectTaskId);
                projectTaskTitle = projectTask?.Title;
            }

            if (entry.DailyTaskId.HasValue)
            {
                var dailyTask = entry.DailyTask ?? await _context.DailyTasks.FindAsync(entry.DailyTaskId);
                dailyTaskTitle = dailyTask?.Title;
            }

            if (entry.ApprovedById.HasValue)
            {
                var approver = entry.ApprovedBy ?? await _context.Users.FindAsync(entry.ApprovedById);
                approvedByName = approver != null ? $"{approver.FirstName} {approver.LastName}" : null;
            }

            return new TaskTimeEntryResponseDto
            {
                Id = entry.Id,
                ProjectTaskId = entry.ProjectTaskId,
                ProjectTaskTitle = projectTaskTitle,
                DailyTaskId = entry.DailyTaskId,
                DailyTaskTitle = dailyTaskTitle,
                UserId = entry.UserId,
                UserName = entry.UserName,
                StartTime = entry.StartTime,
                EndTime = entry.EndTime,
                Duration = entry.Duration,
                Description = entry.Description,
                IsBillable = entry.IsBillable,
                HourlyRate = entry.HourlyRate,
                TotalCost = entry.TotalCost,
                WorkType = entry.WorkType,
                ApprovalStatus = entry.ApprovalStatus,
                ApprovedById = entry.ApprovedById,
                ApprovedByName = approvedByName,
                ApprovedDate = entry.ApprovedDate,
                ApprovalNotes = entry.ApprovalNotes,
                CreatedDate = entry.CreatedDate,
                CreatedBy = entry.CreatedBy,
                ModifiedDate = entry.ModifiedDate,
                ModifiedBy = entry.ModifiedBy
            };
        }
    }
}
