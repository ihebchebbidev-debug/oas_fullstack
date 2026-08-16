using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Models;

namespace MyApi.Modules.Projects.Services
{
    public class RecurringTaskService : IRecurringTaskService
    {
        private readonly ApplicationDbContext _context;

        public RecurringTaskService(ApplicationDbContext context)
        {
            _context = context;
        }

        #region CRUD

        public async Task<RecurringTaskResponseDto> CreateRecurringTaskAsync(CreateRecurringTaskDto dto, string createdBy)
        {
            var recurring = new RecurringTask
            {
                ProjectTaskId = dto.ProjectTaskId,
                DailyTaskId = dto.DailyTaskId,
                RecurrenceType = dto.RecurrenceType,
                DaysOfWeek = dto.DaysOfWeek,
                DayOfMonth = dto.DayOfMonth,
                MonthOfYear = dto.MonthOfYear,
                Interval = dto.Interval > 0 ? dto.Interval : 1,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                MaxOccurrences = dto.MaxOccurrences,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow
            };

            // Calculate first occurrence
            recurring.NextOccurrence = CalculateNextOccurrence(recurring, DateTime.UtcNow);

            _context.RecurringTasks.Add(recurring);
            await _context.SaveChangesAsync();

            return await MapToResponseDto(recurring);
        }

        public async Task<RecurringTaskResponseDto?> GetRecurringTaskByIdAsync(int id)
        {
            var recurring = await _context.RecurringTasks
                .Include(r => r.ProjectTask)
                .Include(r => r.DailyTask)
                .FirstOrDefaultAsync(r => r.Id == id);

            return recurring == null ? null : await MapToResponseDto(recurring);
        }

        public async Task<RecurringTaskResponseDto?> UpdateRecurringTaskAsync(int id, UpdateRecurringTaskDto dto, string modifiedBy)
        {
            var recurring = await _context.RecurringTasks.FindAsync(id);
            if (recurring == null) return null;

            if (dto.RecurrenceType != null) recurring.RecurrenceType = dto.RecurrenceType;
            if (dto.DaysOfWeek != null) recurring.DaysOfWeek = dto.DaysOfWeek;
            if (dto.DayOfMonth.HasValue) recurring.DayOfMonth = dto.DayOfMonth;
            if (dto.MonthOfYear.HasValue) recurring.MonthOfYear = dto.MonthOfYear;
            if (dto.Interval.HasValue && dto.Interval > 0) recurring.Interval = dto.Interval.Value;
            if (dto.StartDate.HasValue) recurring.StartDate = dto.StartDate.Value;
            if (dto.EndDate.HasValue) recurring.EndDate = dto.EndDate;
            if (dto.MaxOccurrences.HasValue) recurring.MaxOccurrences = dto.MaxOccurrences;
            if (dto.IsActive.HasValue) recurring.IsActive = dto.IsActive.Value;
            if (dto.IsPaused.HasValue) recurring.IsPaused = dto.IsPaused.Value;

            recurring.ModifiedBy = modifiedBy;
            recurring.ModifiedDate = DateTime.UtcNow;

            // Recalculate next occurrence
            recurring.NextOccurrence = CalculateNextOccurrence(recurring, recurring.LastGeneratedDate ?? DateTime.UtcNow);

            await _context.SaveChangesAsync();
            return await MapToResponseDto(recurring);
        }

        public async Task<bool> DeleteRecurringTaskAsync(int id)
        {
            var recurring = await _context.RecurringTasks.FindAsync(id);
            if (recurring == null) return false;

            _context.RecurringTasks.Remove(recurring);
            await _context.SaveChangesAsync();
            return true;
        }

        #endregion

        #region Query

        public async Task<List<RecurringTaskResponseDto>> GetRecurringTasksForProjectTaskAsync(int projectTaskId)
        {
            var list = await _context.RecurringTasks
                .Include(r => r.ProjectTask)
                .Where(r => r.ProjectTaskId == projectTaskId)
                .ToListAsync();

            var result = new List<RecurringTaskResponseDto>();
            foreach (var r in list)
            {
                result.Add(await MapToResponseDto(r));
            }
            return result;
        }

        public async Task<List<RecurringTaskResponseDto>> GetRecurringTasksForDailyTaskAsync(int dailyTaskId)
        {
            var list = await _context.RecurringTasks
                .Include(r => r.DailyTask)
                .Where(r => r.DailyTaskId == dailyTaskId)
                .ToListAsync();

            var result = new List<RecurringTaskResponseDto>();
            foreach (var r in list)
            {
                result.Add(await MapToResponseDto(r));
            }
            return result;
        }

        public async Task<List<RecurringTaskResponseDto>> GetAllActiveRecurringTasksAsync()
        {
            var list = await _context.RecurringTasks
                .Include(r => r.ProjectTask)
                .Include(r => r.DailyTask)
                .Where(r => r.IsActive && !r.IsPaused)
                .ToListAsync();

            var result = new List<RecurringTaskResponseDto>();
            foreach (var r in list)
            {
                result.Add(await MapToResponseDto(r));
            }
            return result;
        }

        public async Task<List<RecurringTaskLogResponseDto>> GetLogsForRecurringTaskAsync(int recurringTaskId, int limit = 50)
        {
            return await _context.RecurringTaskLogs
                .Where(l => l.RecurringTaskId == recurringTaskId)
                .OrderByDescending(l => l.GeneratedDate)
                .Take(limit)
                .Select(l => new RecurringTaskLogResponseDto
                {
                    Id = l.Id,
                    RecurringTaskId = l.RecurringTaskId,
                    GeneratedProjectTaskId = l.GeneratedProjectTaskId,
                    GeneratedDailyTaskId = l.GeneratedDailyTaskId,
                    GeneratedDate = l.GeneratedDate,
                    ScheduledFor = l.ScheduledFor,
                    Status = l.Status,
                    Notes = l.Notes
                })
                .ToListAsync();
        }

        #endregion

        #region Actions

        public async Task<RecurringTaskResponseDto?> PauseRecurringTaskAsync(int id, string modifiedBy)
        {
            var recurring = await _context.RecurringTasks.FindAsync(id);
            if (recurring == null) return null;

            recurring.IsPaused = true;
            recurring.ModifiedBy = modifiedBy;
            recurring.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await MapToResponseDto(recurring);
        }

        public async Task<RecurringTaskResponseDto?> ResumeRecurringTaskAsync(int id, string modifiedBy)
        {
            var recurring = await _context.RecurringTasks.FindAsync(id);
            if (recurring == null) return null;

            recurring.IsPaused = false;
            recurring.NextOccurrence = CalculateNextOccurrence(recurring, DateTime.UtcNow);
            recurring.ModifiedBy = modifiedBy;
            recurring.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await MapToResponseDto(recurring);
        }

        #endregion

        #region Generation

        public async Task<GenerateResultDto> GenerateDueTasksAsync(DateTime? upToDate = null, bool dryRun = false)
        {
            var result = new GenerateResultDto();
            var targetDate = upToDate ?? DateTime.UtcNow;

            var dueRecurring = await _context.RecurringTasks
                .Include(r => r.ProjectTask)
                .Include(r => r.DailyTask)
                .Where(r => r.IsActive && !r.IsPaused && r.NextOccurrence <= targetDate)
                .Where(r => r.EndDate == null || r.EndDate >= targetDate)
                .Where(r => r.MaxOccurrences == null || r.OccurrenceCount < r.MaxOccurrences)
                .ToListAsync();

            result.TotalProcessed = dueRecurring.Count;

            foreach (var recurring in dueRecurring)
            {
                try
                {
                    if (dryRun)
                    {
                        result.TasksGenerated++;
                        result.Messages.Add($"Would generate task from recurring #{recurring.Id}");
                        continue;
                    }

                    // Generate the task
                    var (success, generatedTaskId, taskType) = await GenerateTaskFromRecurring(recurring);

                    if (success)
                    {
                        // Log the generation
                        var log = new RecurringTaskLog
                        {
                            RecurringTaskId = recurring.Id,
                            GeneratedProjectTaskId = taskType == "project" ? generatedTaskId : null,
                            GeneratedDailyTaskId = taskType == "daily" ? generatedTaskId : null,
                            GeneratedDate = DateTime.UtcNow,
                            ScheduledFor = recurring.NextOccurrence ?? DateTime.UtcNow,
                            Status = "created"
                        };
                        _context.RecurringTaskLogs.Add(log);

                        // Update recurring task
                        recurring.OccurrenceCount++;
                        recurring.LastGeneratedDate = DateTime.UtcNow;
                        recurring.NextOccurrence = CalculateNextOccurrence(recurring, DateTime.UtcNow);

                        // Check if we've reached max occurrences
                        if (recurring.MaxOccurrences.HasValue && recurring.OccurrenceCount >= recurring.MaxOccurrences)
                        {
                            recurring.IsActive = false;
                        }

                        result.TasksGenerated++;
                        result.Messages.Add($"Generated {taskType} task #{generatedTaskId} from recurring #{recurring.Id}");
                    }
                    else
                    {
                        result.TasksSkipped++;
                        result.Messages.Add($"Skipped recurring #{recurring.Id}: source task not found");
                    }
                }
                catch (Exception ex)
                {
                    result.TasksFailed++;
                    result.Messages.Add($"Failed recurring #{recurring.Id}: {ex.Message}");

                    // Log the failure
                    if (!dryRun)
                    {
                        var log = new RecurringTaskLog
                        {
                            RecurringTaskId = recurring.Id,
                            GeneratedDate = DateTime.UtcNow,
                            ScheduledFor = recurring.NextOccurrence ?? DateTime.UtcNow,
                            Status = "failed",
                            Notes = ex.Message
                        };
                        _context.RecurringTaskLogs.Add(log);
                    }
                }
            }

            if (!dryRun)
            {
                await _context.SaveChangesAsync();
            }

            return result;
        }

        private async Task<(bool success, int? taskId, string taskType)> GenerateTaskFromRecurring(RecurringTask recurring)
        {
            if (recurring.ProjectTaskId.HasValue && recurring.ProjectTask != null)
            {
                var source = recurring.ProjectTask;
                var newTask = new ProjectTask
                {
                    Title = source.Title,
                    Description = source.Description,
                    RelatedEntityType = source.RelatedEntityType,
                    RelatedEntityId = source.RelatedEntityId,
                    Status = source.Status,
                    TaskType = source.TaskType,
                    AssignedUserId = source.AssignedUserId,
                    DueDate = recurring.NextOccurrence?.AddDays(1),
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "RecurringTaskService"
                };

                _context.ProjectTasks.Add(newTask);
                await _context.SaveChangesAsync();
                return (true, newTask.Id, "project");
            }
            else if (recurring.DailyTaskId.HasValue && recurring.DailyTask != null)
            {
                var source = recurring.DailyTask;
                var newTask = new DailyTask
                {
                    Title = source.Title,
                    Description = source.Description,
                    Status = "todo",
                    Priority = source.Priority,
                    AssignedUserId = source.AssignedUserId,
                    DueDate = recurring.NextOccurrence?.AddDays(1) ?? DateTime.UtcNow.AddDays(1),
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "RecurringTaskService"
                };

                _context.DailyTasks.Add(newTask);
                await _context.SaveChangesAsync();
                return (true, newTask.Id, "daily");
            }

            return (false, null, "");
        }

        public async Task<DateTime?> CalculateNextOccurrenceAsync(int recurringTaskId)
        {
            var recurring = await _context.RecurringTasks.FindAsync(recurringTaskId);
            if (recurring == null) return null;

            return CalculateNextOccurrence(recurring, recurring.LastGeneratedDate ?? DateTime.UtcNow);
        }

        #endregion

        #region Helpers

        private DateTime? CalculateNextOccurrence(RecurringTask recurring, DateTime fromDate)
        {
            if (!recurring.IsActive || recurring.IsPaused) return null;

            // Start from the later of StartDate or fromDate
            var baseDate = fromDate < recurring.StartDate ? recurring.StartDate : fromDate;

            DateTime nextDate;

            switch (recurring.RecurrenceType.ToLower())
            {
                case "daily":
                    nextDate = baseDate.Date.AddDays(recurring.Interval);
                    break;

                case "weekly":
                    nextDate = CalculateNextWeeklyOccurrence(baseDate, recurring.DaysOfWeek, recurring.Interval);
                    break;

                case "monthly":
                    nextDate = CalculateNextMonthlyOccurrence(baseDate, recurring.DayOfMonth ?? 1, recurring.Interval);
                    break;

                case "yearly":
                    nextDate = CalculateNextYearlyOccurrence(baseDate, recurring.MonthOfYear ?? 1, recurring.DayOfMonth ?? 1, recurring.Interval);
                    break;

                case "custom":
                    // Custom uses Interval as days
                    nextDate = baseDate.Date.AddDays(recurring.Interval);
                    break;

                default:
                    nextDate = baseDate.Date.AddDays(1);
                    break;
            }

            // Check if we've exceeded end date
            if (recurring.EndDate.HasValue && nextDate > recurring.EndDate.Value)
            {
                return null;
            }

            // Check if we've reached max occurrences
            if (recurring.MaxOccurrences.HasValue && recurring.OccurrenceCount >= recurring.MaxOccurrences)
            {
                return null;
            }

            return nextDate;
        }

        private DateTime CalculateNextWeeklyOccurrence(DateTime fromDate, string? daysOfWeek, int interval)
        {
            if (string.IsNullOrEmpty(daysOfWeek))
            {
                // Default to same day of week
                return fromDate.Date.AddDays(7 * interval);
            }

            var days = daysOfWeek.Split(',').Select(d => int.TryParse(d.Trim(), out int day) ? day : -1).Where(d => d >= 0 && d <= 6).ToList();
            if (!days.Any())
            {
                return fromDate.Date.AddDays(7 * interval);
            }

            var current = fromDate.Date.AddDays(1);
            var weeksPassed = 0;

            while (weeksPassed < 52) // Safety limit
            {
                var dayOfWeek = (int)current.DayOfWeek;
                if (days.Contains(dayOfWeek))
                {
                    if (weeksPassed % interval == 0)
                    {
                        return current;
                    }
                }

                current = current.AddDays(1);
                if (current.DayOfWeek == DayOfWeek.Sunday)
                {
                    weeksPassed++;
                }
            }

            return fromDate.Date.AddDays(7 * interval);
        }

        private DateTime CalculateNextMonthlyOccurrence(DateTime fromDate, int dayOfMonth, int interval)
        {
            var nextMonth = fromDate.AddMonths(interval);
            var targetDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));

            if (dayOfMonth == -1) // Last day of month
            {
                targetDay = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
            }

            return new DateTime(nextMonth.Year, nextMonth.Month, targetDay);
        }

        private DateTime CalculateNextYearlyOccurrence(DateTime fromDate, int monthOfYear, int dayOfMonth, int interval)
        {
            var nextYear = fromDate.Year + interval;
            var targetMonth = Math.Min(Math.Max(monthOfYear, 1), 12);
            var targetDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(nextYear, targetMonth));

            var nextDate = new DateTime(nextYear, targetMonth, targetDay);

            // If the calculated date is before fromDate, add another interval
            if (nextDate <= fromDate)
            {
                nextYear += interval;
                targetDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(nextYear, targetMonth));
                nextDate = new DateTime(nextYear, targetMonth, targetDay);
            }

            return nextDate;
        }

        private async Task<RecurringTaskResponseDto> MapToResponseDto(RecurringTask recurring)
        {
            string? sourceTitle = null;
            if (recurring.ProjectTask != null)
            {
                sourceTitle = recurring.ProjectTask.Title;
            }
            else if (recurring.DailyTask != null)
            {
                sourceTitle = recurring.DailyTask.Title;
            }
            else if (recurring.ProjectTaskId.HasValue)
            {
                var pt = await _context.ProjectTasks.FindAsync(recurring.ProjectTaskId);
                sourceTitle = pt?.Title;
            }
            else if (recurring.DailyTaskId.HasValue)
            {
                var dt = await _context.DailyTasks.FindAsync(recurring.DailyTaskId);
                sourceTitle = dt?.Title;
            }

            return new RecurringTaskResponseDto
            {
                Id = recurring.Id,
                ProjectTaskId = recurring.ProjectTaskId,
                DailyTaskId = recurring.DailyTaskId,
                RecurrenceType = recurring.RecurrenceType,
                DaysOfWeek = recurring.DaysOfWeek,
                DayOfMonth = recurring.DayOfMonth,
                MonthOfYear = recurring.MonthOfYear,
                Interval = recurring.Interval,
                StartDate = recurring.StartDate,
                EndDate = recurring.EndDate,
                MaxOccurrences = recurring.MaxOccurrences,
                OccurrenceCount = recurring.OccurrenceCount,
                NextOccurrence = recurring.NextOccurrence,
                LastGeneratedDate = recurring.LastGeneratedDate,
                IsActive = recurring.IsActive,
                IsPaused = recurring.IsPaused,
                CreatedDate = recurring.CreatedDate,
                CreatedBy = recurring.CreatedBy,
                SourceTaskTitle = sourceTitle,
                RecurrenceDescription = BuildRecurrenceDescription(recurring)
            };
        }

        private string BuildRecurrenceDescription(RecurringTask recurring)
        {
            var interval = recurring.Interval > 1 ? $"Every {recurring.Interval} " : "Every ";

            return recurring.RecurrenceType.ToLower() switch
            {
                "daily" => recurring.Interval == 1 ? "Daily" : $"Every {recurring.Interval} days",
                "weekly" when !string.IsNullOrEmpty(recurring.DaysOfWeek) =>
                    $"{interval}week on {FormatDaysOfWeek(recurring.DaysOfWeek)}",
                "weekly" => recurring.Interval == 1 ? "Weekly" : $"Every {recurring.Interval} weeks",
                "monthly" when recurring.DayOfMonth == -1 => $"{interval}month on the last day",
                "monthly" => $"{interval}month on day {recurring.DayOfMonth}",
                "yearly" => $"{interval}year on {FormatMonthDay(recurring.MonthOfYear, recurring.DayOfMonth)}",
                "custom" => $"Every {recurring.Interval} days",
                _ => "Custom recurrence"
            };
        }

        private string FormatDaysOfWeek(string daysOfWeek)
        {
            var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            var days = daysOfWeek.Split(',')
                .Select(d => int.TryParse(d.Trim(), out int day) && day >= 0 && day <= 6 ? dayNames[day] : null)
                .Where(d => d != null);
            return string.Join(", ", days);
        }

        private string FormatMonthDay(int? month, int? day)
        {
            var monthNames = new[] { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            var m = month.HasValue && month >= 1 && month <= 12 ? monthNames[month.Value] : "Jan";
            var d = day ?? 1;
            return $"{m} {d}";
        }

        #endregion
    }
}
