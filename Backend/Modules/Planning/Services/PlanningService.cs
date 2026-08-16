using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.Planning.DTOs;
using MyApi.Modules.Planning.Models;
using MyApi.Modules.ServiceOrders.Models;
using MyApi.Modules.Dispatches.Models;
using MyApi.Modules.Dispatches.Services;
using MyApi.Modules.Dispatches.DTOs;
using MyApi.Modules.Contacts.Models;
using MyApi.Modules.Settings.Services;

namespace MyApi.Modules.Planning.Services
{
    public class PlanningService : IPlanningService
    {
        private readonly ApplicationDbContext _db;
        private readonly IDispatchService _dispatchService;
        private readonly ILogger<PlanningService> _logger;
        private readonly IAppSettingsService? _appSettingsService;

        public PlanningService(
            ApplicationDbContext db,
            IDispatchService dispatchService,
            ILogger<PlanningService> logger,
            IAppSettingsService? appSettingsService = null)
        {
            _db = db;
            _dispatchService = dispatchService;
            _logger = logger;
            _appSettingsService = appSettingsService;
        }

        public async Task<AssignJobResponseDto> AssignJobAsync(AssignJobDto dto, string currentUserId)
        {
            // 1. Validate assignment
            var validation = await ValidateAssignmentAsync(new ValidateAssignmentDto
            {
                JobId = dto.JobId,
                TechnicianIds = dto.TechnicianIds,
                ScheduledDate = dto.ScheduledDate,
                ScheduledStartTime = dto.ScheduledStartTime,
                ScheduledEndTime = dto.ScheduledEndTime,
                AllowOverlap = dto.AllowOverlap
            });

            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Assignment validation failed: {string.Join(", ", validation.Conflicts.Select(c => c.Message))}");
            }

            // 2. Update job
            var job = await _db.ServiceOrderJobs
                .Include(j => j.ServiceOrder)
                .FirstOrDefaultAsync(j => j.Id == dto.JobId);
                
            if (job == null)
                throw new KeyNotFoundException($"Job {dto.JobId} not found");

            job.AssignedTechnicianIds = dto.TechnicianIds.ToArray();
            job.ScheduledDate = dto.ScheduledDate;
            job.Status = "scheduled";
            job.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // 3. Create dispatch if requested
            object? dispatch = null;
            if (dto.AutoCreateDispatch)
            {
                try
                {
                    // Read JobConversionMode setting (defaults to "installation" when not set)
                    string conversionMode = "installation";
                    if (_appSettingsService != null)
                    {
                        try
                        {
                            var v = await _appSettingsService.GetSettingAsync("JobConversionMode");
                            if (!string.IsNullOrWhiteSpace(v)) conversionMode = v;
                        }
                        catch (Exception cex)
                        {
                            _logger.LogWarning(cex, "Failed to read JobConversionMode; defaulting to 'installation'");
                        }
                    }

                    // When installation mode AND the job belongs to an installation, find-or-create
                    // a single installation dispatch and append this job to it instead of creating
                    // a new per-job dispatch.
                    if (conversionMode == "installation"
                        && job.InstallationId.HasValue)
                    {
                        var installationIdInt = job.InstallationId.Value;
                        dispatch = await _dispatchService.AddJobsToInstallationDispatchAsync(
                            installationIdInt,
                            job.InstallationName ?? $"Installation #{installationIdInt}",
                            new List<int> { dto.JobId },
                            dto.TechnicianIds,
                            dto.ScheduledDate,
                            dto.ScheduledStartTime,
                            dto.ScheduledEndTime,
                            dto.Priority,
                            null,
                            null,
                            null,
                            job.ServiceOrderId,
                            currentUserId);
                    }
                    else
                    {
                        var createDispatchDto = new CreateDispatchFromJobDto
                        {
                            AssignedTechnicianIds = dto.TechnicianIds,
                            ScheduledDate = dto.ScheduledDate,
                            ScheduledStartTime = dto.ScheduledStartTime,
                            ScheduledEndTime = dto.ScheduledEndTime,
                            Priority = dto.Priority
                        };

                        dispatch = await _dispatchService.CreateFromJobAsync(dto.JobId, createDispatchDto, currentUserId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create dispatch for job {JobId}", dto.JobId);
                    // Continue without dispatch creation
                }
            }

            // Get contact info from service order
            Contact? contact = null;
            if (job.ServiceOrder != null)
            {
                contact = await _db.Contacts.FindAsync(job.ServiceOrder.ContactId);
            }

            // 4. Return response
            return new AssignJobResponseDto
            {
                Job = MapJobToDto(job, contact),
                Dispatch = dispatch
            };
        }

        public async Task<BatchAssignResponseDto> BatchAssignAsync(BatchAssignDto dto, string currentUserId)
        {
            var response = new BatchAssignResponseDto();

            foreach (var assignment in dto.Assignments)
            {
                try
                {
                    assignment.AutoCreateDispatch = dto.AutoCreateDispatches;
                    var result = await AssignJobAsync(assignment, currentUserId);
                    
                    response.Successful++;
                    response.Results.Add(new BatchAssignResult
                    {
                        JobId = assignment.JobId,
                        Status = "success",
                        DispatchId = result.Dispatch != null ? (result.Dispatch as DispatchDto)?.Id : null
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to assign job {JobId} in batch", assignment.JobId);
                    response.Failed++;
                    response.Results.Add(new BatchAssignResult
                    {
                        JobId = assignment.JobId,
                        Status = "failed",
                        ErrorMessage = ex.Message
                    });
                }
            }

            return response;
        }

        public async Task<AssignmentValidationResult> ValidateAssignmentAsync(ValidateAssignmentDto dto)
        {
            var result = new AssignmentValidationResult { IsValid = true };

            // 1. Check job exists
            var job = await _db.ServiceOrderJobs.FirstOrDefaultAsync(j => j.Id == dto.JobId);
            if (job == null)
            {
                result.IsValid = false;
                result.Conflicts.Add(new AssignmentConflict
                {
                    Type = "job_not_found",
                    Message = $"Job {dto.JobId} not found"
                });
                return result;
            }

            // 2. Check each user/technician
            foreach (var userId in dto.TechnicianIds)
            {
                if (!int.TryParse(userId, out int userIdInt))
                {
                    result.IsValid = false;
                    result.Conflicts.Add(new AssignmentConflict
                    {
                        Type = "invalid_user_id",
                        Message = $"Invalid user ID: {userId}"
                    });
                    continue;
                }

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userIdInt && u.Role == "technician");
                if (user == null)
                {
                    result.IsValid = false;
                    result.Conflicts.Add(new AssignmentConflict
                    {
                        Type = "user_not_found",
                        Message = $"User {userId} not found"
                    });
                    continue;
                }

                // 3. Check user on leave
                var onLeave = await _db.Set<UserLeave>()
                    .AnyAsync(l =>
                        l.UserId == userIdInt &&
                        l.Status == "approved" &&
                        l.StartDate <= dto.ScheduledDate.Date &&
                        l.EndDate >= dto.ScheduledDate.Date);

                if (onLeave)
                {
                    result.IsValid = false;
                    result.Conflicts.Add(new AssignmentConflict
                    {
                        Type = "on_leave",
                        Message = $"User {user.FirstName} {user.LastName} is on leave on {dto.ScheduledDate:yyyy-MM-dd}"
                    });
                    continue;
                }

                // 4. Check time conflicts with existing dispatches (overlap by time window)
                var sameDayDispatches = await _db.Dispatches
                    .Include(d => d.AssignedTechnicians)
                    .Where(d =>
                        d.AssignedTechnicians.Any(at => at.TechnicianId == userIdInt) &&
                        d.ScheduledDate.Date == dto.ScheduledDate.Date &&
                        !d.IsDeleted &&
                        d.Status != "cancelled" &&
                        d.Status != "completed")
                    .ToListAsync();

                var conflictingDispatches = sameDayDispatches
                    .Where(d =>
                    {
                        var s = d.ScheduledStartTime ?? TimeSpan.Zero;
                        var e = d.ScheduledEndTime ?? TimeSpan.Zero;
                        if (e <= s) return true; // unknown window — treat as conflict
                        return s < dto.ScheduledEndTime && dto.ScheduledStartTime < e;
                    })
                    .ToList();

                foreach (var cd in conflictingDispatches)
                {
                    var msg = $"User {user.FirstName} {user.LastName} already has dispatch {cd.DispatchNumber} scheduled on {dto.ScheduledDate:yyyy-MM-dd}";
                    result.Warnings.Add(msg);
                    if (!dto.AllowOverlap)
                    {
                        result.IsValid = false;
                        result.Conflicts.Add(new AssignmentConflict
                        {
                            Type = "time_overlap",
                            Message = msg,
                            ConflictingData = new { cd.Id, cd.DispatchNumber, cd.ScheduledDate, cd.ScheduledStartTime, cd.ScheduledEndTime }
                        });
                    }
                }
            }

            return result;
        }

        public async Task<Planning.DTOs.PagedResult<ServiceOrderJobDto>> GetUnassignedJobsAsync(
            string? priority,
            List<string>? requiredSkills,
            string? serviceOrderId,
            int page,
            int pageSize)
        {
            var query = _db.ServiceOrderJobs
                .Include(j => j.ServiceOrder)
                .Where(j => j.Status == "unscheduled" || j.Status == "unassigned");

            if (!string.IsNullOrEmpty(priority))
                query = query.Where(j => j.Priority == priority);

            if (!string.IsNullOrEmpty(serviceOrderId) && int.TryParse(serviceOrderId, out int soId))
                query = query.Where(j => j.ServiceOrderId == soId);

            // Apply required-skills filter (any overlap between job-required skills and requested skills)
            if (requiredSkills != null && requiredSkills.Count > 0)
            {
                query = query.Where(j =>
                    j.RequiredSkills != null &&
                    j.RequiredSkills.Any(rs => requiredSkills.Contains(rs)));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(j => j.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get contacts for jobs via service orders
            var contactIds = items
                .Where(j => j.ServiceOrder != null)
                .Select(j => j.ServiceOrder!.ContactId)
                .Distinct()
                .ToList();
            var contacts = await _db.Contacts
                .Where(c => contactIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            return new Planning.DTOs.PagedResult<ServiceOrderJobDto>
            {
                Data = items.Select(j => MapJobToDto(j, j.ServiceOrder != null ? contacts.GetValueOrDefault(j.ServiceOrder.ContactId) : null)).ToList(),
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }

        public async Task<UserScheduleDto> GetUserScheduleAsync(
            string userId,
            DateTime startDate,
            DateTime endDate)
        {
            if (!int.TryParse(userId, out int userIdInt))
                throw new ArgumentException("Invalid user ID");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userIdInt);
            if (user == null)
                throw new KeyNotFoundException($"User {userId} not found");

            // Get working hours
            var workingHours = await _db.Set<UserWorkingHours>()
                .Where(wh => wh.UserId == userIdInt && wh.IsActive)
                .ToListAsync();

            // Get dispatches - filter by user ID as int
            var dispatches = await _db.Dispatches
                .Include(d => d.AssignedTechnicians)
                .Where(d =>
                    d.AssignedTechnicians.Any(at => at.TechnicianId == userIdInt) &&
                    d.ScheduledDate >= startDate.Date &&
                    d.ScheduledDate <= endDate.Date &&
                    !d.IsDeleted)
                .ToListAsync();

            // Get leaves
            var leaves = await _db.Set<UserLeave>()
                .Where(l =>
                    l.UserId == userIdInt &&
                    l.Status == "approved" &&
                    l.StartDate <= endDate.Date &&
                    l.EndDate >= startDate.Date)
                .ToListAsync();

            // Resolve actual job titles in one batch query
            var jobIds = dispatches
                .Where(d => !string.IsNullOrEmpty(d.JobId) && int.TryParse(d.JobId, out _))
                .Select(d => int.Parse(d.JobId!))
                .Distinct()
                .ToList();

            var jobTitles = jobIds.Count > 0
                ? await _db.ServiceOrderJobs
                    .Where(j => jobIds.Contains(j.Id))
                    .ToDictionaryAsync(j => j.Id, j => j.Title ?? string.Empty)
                : new Dictionary<int, string>();

            // Build response
            return new UserScheduleDto
            {
                UserId = userId,
                UserName = $"{user.FirstName} {user.LastName}",
                WorkingHours = BuildWorkingHoursDict(workingHours),
                Dispatches = dispatches.Select(d => MapDispatchToScheduleDto(d, jobTitles)).ToList(),
                Leaves = leaves.Select(MapLeaveToDto).ToList(),
                TotalScheduledHours = CalculateTotalScheduledHours(dispatches),
                AvailableHours = CalculateAvailableHours(workingHours, dispatches, startDate, endDate)
            };
        }

        public async Task<List<UserAvailabilityDto>> GetAvailableUsersAsync(
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime,
            List<string>? requiredSkills)
        {
            var users = await _db.Users
                .Where(u => u.Role == "technician" && u.IsActive)
                .ToListAsync();

            // Pre-filter by required skills (in-memory: skills is a JSON/CSV string)
            if (requiredSkills != null && requiredSkills.Count > 0)
            {
                users = users.Where(u =>
                {
                    var s = ParseSkillsString(u.Skills);
                    return requiredSkills.All(rs => s.Contains(rs));
                }).ToList();
            }

            if (users.Count == 0) return new List<UserAvailabilityDto>();

            var userIds = users.Select(u => u.Id).ToList();
            var dayOfWeek = (int)date.DayOfWeek;

            // Batch: leaves, dispatches, working hours — one round trip each
            var onLeaveIds = await _db.Set<UserLeave>()
                .Where(l =>
                    userIds.Contains(l.UserId) &&
                    l.Status == "approved" &&
                    l.StartDate <= date.Date &&
                    l.EndDate >= date.Date)
                .Select(l => l.UserId)
                .ToListAsync();

            var allDispatches = await _db.Dispatches
                .Include(d => d.AssignedTechnicians)
                .Where(d =>
                    d.ScheduledDate.Date == date.Date &&
                    !d.IsDeleted &&
                    d.Status != "cancelled" &&
                    d.Status != "completed" &&
                    d.AssignedTechnicians.Any(at => userIds.Contains(at.TechnicianId)))
                .ToListAsync();

            var workingHoursList = await _db.Set<UserWorkingHours>()
                .Where(wh => userIds.Contains(wh.UserId) && wh.DayOfWeek == dayOfWeek && wh.IsActive)
                .ToListAsync();
            var whByUser = workingHoursList.ToDictionary(w => w.UserId);

            var availabilityList = new List<UserAvailabilityDto>();
            foreach (var user in users)
            {
                if (onLeaveIds.Contains(user.Id)) continue;

                var dispatches = allDispatches
                    .Where(d => d.AssignedTechnicians.Any(at => at.TechnicianId == user.Id))
                    .ToList();

                var scheduledMinutes = dispatches
                    .Where(d => d.ActualDuration.HasValue)
                    .Sum(d => d.ActualDuration!.Value);

                whByUser.TryGetValue(user.Id, out var workingHoursEntry);
                var availableMinutes = workingHoursEntry != null
                    ? (workingHoursEntry.EndTime - workingHoursEntry.StartTime).TotalMinutes - scheduledMinutes
                    : 0;

                var isAvailable = availableMinutes >= (endTime - startTime).TotalMinutes;

                availabilityList.Add(new UserAvailabilityDto
                {
                    Id = user.Id.ToString(),
                    Name = $"{user.FirstName} {user.LastName}",
                    Email = user.Email,
                    Skills = ParseSkillsString(user.Skills),
                    Status = user.CurrentStatus ?? "offline",
                    IsAvailable = isAvailable,
                    AvailableMinutes = (int)availableMinutes,
                    ScheduledMinutes = (int)scheduledMinutes,
                    UtilizationPercentage = workingHoursEntry != null
                        ? (decimal)(scheduledMinutes / (workingHoursEntry.EndTime - workingHoursEntry.StartTime).TotalMinutes * 100)
                        : 0
                });
            }

            return availabilityList.OrderByDescending(a => a.IsAvailable).ThenBy(a => a.ScheduledMinutes).ToList();
        }

        // Helper methods
        private ServiceOrderJobDto MapJobToDto(ServiceOrderJob job, Contact? contact)
        {
            return new ServiceOrderJobDto
            {
                Id = job.Id,
                ServiceOrderId = job.ServiceOrderId,
                Title = job.Title ?? string.Empty,
                Description = job.Description,
                Status = job.Status,
                Priority = job.Priority ?? "medium",
                EstimatedDuration = job.EstimatedDuration,
                RequiredSkills = job.RequiredSkills?.ToList(),
                AssignedTechnicianIds = job.AssignedTechnicianIds?.ToList() ?? new List<string>(),
                ScheduledDate = job.ScheduledDate,
                Location = null,
                Contact = contact != null ? new ContactInfoDto
                {
                    Id = contact.Id,
                    Name = $"{contact.FirstName} {contact.LastName}".Trim(),
                    Phone = contact.Phone,
                    Email = contact.Email,
                    Company = contact.Company
                } : null,
                CreatedAt = job.UpdatedAt ?? DateTime.UtcNow,
                UpdatedAt = job.UpdatedAt ?? DateTime.UtcNow
            };
        }

        private Dictionary<string, WorkingHoursDto?> BuildWorkingHoursDict(List<UserWorkingHours> hours)
        {
            var days = new[] { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };
            var dict = new Dictionary<string, WorkingHoursDto?>();

            foreach (var day in days.Select((name, index) => (name, index)))
            {
                var wh = hours.FirstOrDefault(h => h.DayOfWeek == day.index);
                dict[day.name] = wh == null ? null : new WorkingHoursDto
                {
                    Start = wh.StartTime.ToString(@"hh\:mm"),
                    End = wh.EndTime.ToString(@"hh\:mm")
                };
            }

            return dict;
        }

        private DispatchScheduleDto MapDispatchToScheduleDto(Dispatch dispatch, Dictionary<int, string>? jobTitles = null)
        {
            var jobId = int.TryParse(dispatch.JobId, out var jid) ? jid : (int?)null;
            var jobTitle = jobId.HasValue && jobTitles != null && jobTitles.TryGetValue(jobId.Value, out var t) && !string.IsNullOrEmpty(t)
                ? t
                : dispatch.DispatchNumber;

            return new DispatchScheduleDto
            {
                Id = dispatch.Id,
                DispatchNumber = dispatch.DispatchNumber,
                JobId = jobId,
                JobTitle = jobTitle,
                ServiceOrderId = dispatch.ServiceOrderId,
                ScheduledDate = dispatch.ScheduledDate,
                ScheduledStartTime = dispatch.ScheduledStartTime ?? TimeSpan.Zero,
                ScheduledEndTime = dispatch.ScheduledEndTime ?? TimeSpan.Zero,
                EstimatedDuration = dispatch.ActualDuration ?? 0,
                Status = dispatch.Status,
                Priority = dispatch.Priority
            };
        }

        private UserLeaveDto MapLeaveToDto(UserLeave leave)
        {
            return new UserLeaveDto
            {
                Id = leave.Id,
                LeaveType = leave.LeaveType,
                StartDate = leave.StartDate,
                EndDate = leave.EndDate,
                Status = leave.Status,
                Reason = leave.Reason
            };
        }

        private decimal CalculateTotalScheduledHours(List<Dispatch> dispatches)
        {
            decimal total = 0;
            foreach (var d in dispatches)
            {
                if (d.ActualDuration.HasValue)
                {
                    total += d.ActualDuration.Value / 60m;
                }
            }
            return total;
        }

        private decimal CalculateAvailableHours(List<UserWorkingHours> workingHours, List<Dispatch> dispatches, DateTime startDate, DateTime endDate)
        {
            var totalWorkingHours = 0m;
            var currentDate = startDate.Date;

            while (currentDate <= endDate.Date)
            {
                var dayOfWeek = (int)currentDate.DayOfWeek;
                var wh = workingHours.FirstOrDefault(w => w.DayOfWeek == dayOfWeek);
                if (wh != null)
                {
                    totalWorkingHours += (decimal)(wh.EndTime - wh.StartTime).TotalHours;
                }
                currentDate = currentDate.AddDays(1);
            }

            return totalWorkingHours - CalculateTotalScheduledHours(dispatches);
        }

        private List<string> ParseSkillsString(string? skillsJson)
        {
            if (string.IsNullOrEmpty(skillsJson))
                return new List<string>();

            try
            {
                var skills = System.Text.Json.JsonSerializer.Deserialize<List<string>>(skillsJson);
                return skills ?? new List<string>();
            }
            catch
            {
                return skillsJson.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();
            }
        }

        // ===================== SCHEDULE MANAGEMENT =====================

        public async Task<UserFullScheduleDto> GetUserFullScheduleAsync(int userId)
        {
            // Check both Users and MainAdminUsers tables
            // MainAdminUser has id=1, regular Users have id>=2
            string? firstName = null;
            string? lastName = null;
            
            if (userId == 1)
            {
                // Check MainAdminUsers table first for id=1
                var adminUser = await _db.MainAdminUsers.FirstOrDefaultAsync(u => u.Id == userId);
                if (adminUser != null)
                {
                    firstName = adminUser.FirstName;
                    lastName = adminUser.LastName;
                }
            }
            
            if (firstName == null)
            {
                // Check regular Users table
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null)
                {
                    firstName = user.FirstName;
                    lastName = user.LastName;
                }
            }
            
            if (firstName == null && lastName == null)
                throw new KeyNotFoundException($"User {userId} not found");

            // Get working hours - fetch ALL entries, not just active ones
            // The Enabled/IsActive state is returned in the DTO, don't filter by it
            var workingHours = await _db.Set<UserWorkingHours>()
                .Where(wh => wh.UserId == userId)
                .ToListAsync();

            // Get leaves
            var leaves = await _db.Set<UserLeave>()
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            // Get status from history or default
            var latestStatus = await _db.Set<UserStatusHistory>()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.ChangedAt)
                .FirstOrDefaultAsync();

            // Build day schedules
            var daySchedules = new Dictionary<int, DayScheduleDto>();
            for (int day = 0; day <= 6; day++)
            {
                var wh = workingHours.FirstOrDefault(w => w.DayOfWeek == day);
                if (wh != null)
                {
                    daySchedules[day] = new DayScheduleDto
                    {
                        Enabled = wh.IsActive,
                        StartTime = wh.StartTime.ToString(@"hh\:mm"),
                        EndTime = wh.EndTime.ToString(@"hh\:mm"),
                        FullDayOff = !wh.IsActive
                    };
                }
                else
                {
                    var isWeekend = day == 0 || day == 6;
                    daySchedules[day] = new DayScheduleDto
                    {
                        Enabled = !isWeekend,
                        StartTime = "08:00",
                        EndTime = "17:00",
                        FullDayOff = isWeekend
                    };
                }
            }

            return new UserFullScheduleDto
            {
                UserId = userId,
                UserName = $"{firstName} {lastName}",
                Status = latestStatus?.NewStatus ?? "available",
                ScheduleNote = null,
                DaySchedules = daySchedules,
                Leaves = leaves.Select(l => new UserLeaveDto
                {
                    Id = l.Id,
                    LeaveType = l.LeaveType,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    Status = l.Status,
                    Reason = l.Reason
                }).ToList()
            };
        }

        public async Task<UserFullScheduleDto> UpdateUserScheduleAsync(UpdateUserScheduleDto dto)
        {
            // Check both Users and MainAdminUsers tables
            bool userExists = false;
            
            if (dto.UserId == 1)
            {
                userExists = await _db.MainAdminUsers.AnyAsync(u => u.Id == dto.UserId);
            }
            
            if (!userExists)
            {
                userExists = await _db.Users.AnyAsync(u => u.Id == dto.UserId);
            }
            
            if (!userExists)
                throw new KeyNotFoundException($"User {dto.UserId} not found");

            // Update working hours if provided
            if (dto.DaySchedules != null)
            {
                var existingHours = await _db.Set<UserWorkingHours>()
                    .Where(wh => wh.UserId == dto.UserId)
                    .ToListAsync();

                foreach (var (dayOfWeek, schedule) in dto.DaySchedules)
                {
                    var existing = existingHours.FirstOrDefault(wh => wh.DayOfWeek == dayOfWeek);
                    
                    if (existing != null)
                    {
                        existing.IsActive = schedule.Enabled && !schedule.FullDayOff;
                        existing.StartTime = TimeSpan.Parse(schedule.StartTime);
                        existing.EndTime = TimeSpan.Parse(schedule.EndTime);
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _db.Set<UserWorkingHours>().Add(new UserWorkingHours
                        {
                            UserId = dto.UserId,
                            DayOfWeek = dayOfWeek,
                            IsActive = schedule.Enabled && !schedule.FullDayOff,
                            StartTime = TimeSpan.Parse(schedule.StartTime),
                            EndTime = TimeSpan.Parse(schedule.EndTime),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            // Update status if provided
            if (!string.IsNullOrEmpty(dto.Status))
            {
                _db.Set<UserStatusHistory>().Add(new UserStatusHistory
                {
                    UserId = dto.UserId,
                    NewStatus = dto.Status,
                    ChangedAt = DateTime.UtcNow,
                    Reason = dto.ScheduleNote
                });
            }

            await _db.SaveChangesAsync();

            return await GetUserFullScheduleAsync(dto.UserId);
        }

        // ===================== LEAVE MANAGEMENT =====================

        private static decimal LeaveDayCount(DateTime start, DateTime end)
            => (decimal)(end.Date - start.Date).TotalDays + 1m;

        /// <summary>
        /// Server-side leave validation shared by create/update:
        ///  - end date cannot precede start date;
        ///  - a pending/approved leave cannot overlap another pending/approved
        ///    leave for the same employee (double-booking corrupts payroll days);
        ///  - the request cannot exceed the remaining annual allowance when an
        ///    HR balance row exists for that (user, year, leave type).
        /// Cancelled/rejected rows are ignored on both checks.
        /// </summary>
        private async Task ValidateLeaveAsync(int userId, string leaveType, DateTime startDate, DateTime endDate, string status, int? excludeLeaveId)
        {
            // `user_leaves.start_date/end_date` are `timestamp with time zone`, and Npgsql
            // refuses to bind a DateTime whose Kind is Unspecified/Local. Values coming from
            // the DTO (JSON without offset) or from a tracked entity can carry Unspecified,
            // so every value used as a query parameter is normalized to a UTC day first.
            var rangeStart = AsUtcDay(startDate);
            var rangeEnd = AsUtcDay(endDate);
            if (rangeEnd < rangeStart)
                throw new InvalidOperationException("planning.leave_end_before_start");

            var normalizedStatus = (status ?? "pending").ToLowerInvariant();
            if (normalizedStatus is "rejected" or "cancelled" or "canceled")
                return;

            // Overlap must only be tested against leaves that are actually granted.
            // Two employees' pending requests can legitimately overlap in the inbox, and
            // blocking on them made every "Approve" click fail (surfaced as HTTP 500).
            var overlapEnd = rangeEnd.AddDays(1);
            var overlaps = await _db.Set<UserLeave>().AsNoTracking()
                .Where(l => l.UserId == userId
                            && (excludeLeaveId == null || l.Id != excludeLeaveId.Value)
                            && l.Status == "approved"
                            && l.StartDate < overlapEnd
                            && l.EndDate >= rangeStart)
                .AnyAsync();
            if (overlaps)
                throw new InvalidOperationException("planning.leave_overlap");

            var activeStatuses = new[] { "pending", "approved" };


            var year = rangeStart.Year;
            var balance = await _db.Set<MyApi.Modules.HR.Models.HrLeaveBalance>().AsNoTracking()
                .FirstOrDefaultAsync(b => b.UserId == userId && b.Year == year && b.LeaveType == leaveType);
            if (balance == null || balance.AnnualAllowance <= 0) return;

            var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextYearStart = yearStart.AddYears(1);
            var sameYear = await _db.Set<UserLeave>().AsNoTracking()
                .Where(l => l.UserId == userId
                            && l.LeaveType == leaveType
                            && (excludeLeaveId == null || l.Id != excludeLeaveId.Value)
                            && activeStatuses.Contains(l.Status)
                            && l.StartDate < nextYearStart
                            && l.EndDate >= yearStart)
                .ToListAsync();

            var alreadyBooked = sameYear.Sum(l => LeaveDayCount(l.StartDate, l.EndDate));
            var requested = LeaveDayCount(rangeStart, rangeEnd);

            if (alreadyBooked + requested > balance.AnnualAllowance)
                throw new InvalidOperationException("planning.leave_allowance_exceeded");
        }

        private static DateTime AsUtcDay(DateTime value)
            => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

        public async Task<UserLeaveDto> CreateLeaveAsync(CreateLeaveDto dto)
        {
            // User-initiated transactions must run inside the execution strategy,
            // otherwise Npgsql's retrying strategy throws and the request 500s.
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => CreateLeaveCoreAsync(dto));
        }

        private async Task<UserLeaveDto> CreateLeaveCoreAsync(CreateLeaveDto dto)
        {
            var status = string.IsNullOrWhiteSpace(dto.Status) ? "pending" : dto.Status!.ToLowerInvariant();
            // PG columns are `timestamp with time zone` — Npgsql rejects Unspecified/Local kinds.
            var startDate = AsUtcDay(dto.StartDate);
            var endDate = AsUtcDay(dto.EndDate);

            // Serialize concurrent submissions for the same employee so two
            // overlapping requests cannot both pass the validation below.
            await using var tx = await _db.Database.BeginTransactionAsync();
            const int LeaveLockNamespace = 0x484C5645; // 'HLVE'
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0}, {1})", LeaveLockNamespace, dto.UserId);

            await ValidateLeaveAsync(dto.UserId, dto.LeaveType, startDate, endDate, status, null);

            var leave = new UserLeave
            {
                UserId = dto.UserId,
                LeaveType = dto.LeaveType,
                StartDate = startDate,
                EndDate = endDate,
                Reason = dto.Reason,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Set<UserLeave>().Add(leave);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return new UserLeaveDto
            {
                Id = leave.Id,
                LeaveType = leave.LeaveType,
                StartDate = leave.StartDate,
                EndDate = leave.EndDate,
                Status = leave.Status,
                Reason = leave.Reason
            };
        }

        public async Task<UserLeaveDto> UpdateLeaveAsync(int leaveId, UpdateLeaveDto dto)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => UpdateLeaveCoreAsync(leaveId, dto));
        }

        private async Task<UserLeaveDto> UpdateLeaveCoreAsync(int leaveId, UpdateLeaveDto dto)
        {
            var leave = await _db.Set<UserLeave>().FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                throw new KeyNotFoundException($"Leave {leaveId} not found");

            await using var tx = await _db.Database.BeginTransactionAsync();
            const int LeaveLockNamespace = 0x484C5645; // 'HLVE'
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0}, {1})", LeaveLockNamespace, leave.UserId);

            if (!string.IsNullOrEmpty(dto.LeaveType))
                leave.LeaveType = dto.LeaveType;
            if (dto.StartDate.HasValue)
                leave.StartDate = AsUtcDay(dto.StartDate.Value);
            if (dto.EndDate.HasValue)
                leave.EndDate = AsUtcDay(dto.EndDate.Value);
            // Rows written before this normalization (or read back with Kind=Unspecified)
            // must be coerced to UTC as well, otherwise SaveChanges/validation throws.
            leave.StartDate = AsUtcDay(leave.StartDate);
            leave.EndDate = AsUtcDay(leave.EndDate);

            if (!string.IsNullOrEmpty(dto.Reason))
                leave.Reason = dto.Reason;
            if (!string.IsNullOrEmpty(dto.Status))
                leave.Status = dto.Status.ToLowerInvariant();

            await ValidateLeaveAsync(leave.UserId, leave.LeaveType, leave.StartDate, leave.EndDate, leave.Status, leave.Id);

            leave.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return new UserLeaveDto
            {
                Id = leave.Id,
                LeaveType = leave.LeaveType,
                StartDate = leave.StartDate,
                EndDate = leave.EndDate,
                Status = leave.Status,
                Reason = leave.Reason
            };
        }


        public async Task DeleteLeaveAsync(int leaveId)
        {
            var leave = await _db.Set<UserLeave>().FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                throw new KeyNotFoundException($"Leave {leaveId} not found");

            _db.Set<UserLeave>().Remove(leave);
            await _db.SaveChangesAsync();
        }

        public async Task<List<UserLeaveDto>> GetUserLeavesAsync(int userId)
        {
            var leaves = await _db.Set<UserLeave>()
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            return leaves.Select(l => new UserLeaveDto
            {
                Id = l.Id,
                LeaveType = l.LeaveType,
                StartDate = l.StartDate,
                EndDate = l.EndDate,
                Status = l.Status,
                Reason = l.Reason
            }).ToList();
        }
    }
}
