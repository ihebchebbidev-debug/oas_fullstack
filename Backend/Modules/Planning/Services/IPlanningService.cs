using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyApi.Modules.Planning.DTOs;

namespace MyApi.Modules.Planning.Services
{
    public interface IPlanningService
    {
        // Job Assignment
        Task<AssignJobResponseDto> AssignJobAsync(AssignJobDto dto, string currentUserId);
        Task<BatchAssignResponseDto> BatchAssignAsync(BatchAssignDto dto, string currentUserId);
        Task<AssignmentValidationResult> ValidateAssignmentAsync(ValidateAssignmentDto dto);

        // Unassigned Jobs
        Task<PagedResult<ServiceOrderJobDto>> GetUnassignedJobsAsync(
            string? priority,
            List<string>? requiredSkills,
            string? serviceOrderId,
            int page,
            int pageSize
        );

        // User Schedule
        Task<UserScheduleDto> GetUserScheduleAsync(
            string userId,
            DateTime startDate,
            DateTime endDate
        );

        // Available Users
        Task<List<UserAvailabilityDto>> GetAvailableUsersAsync(
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime,
            List<string>? requiredSkills
        );

        // Working Hours Management
        Task<UserFullScheduleDto> GetUserFullScheduleAsync(int userId);
        Task<UserFullScheduleDto> UpdateUserScheduleAsync(UpdateUserScheduleDto dto);
        
        // Leave Management
        Task<UserLeaveDto> CreateLeaveAsync(CreateLeaveDto dto);
        Task<UserLeaveDto> UpdateLeaveAsync(int leaveId, UpdateLeaveDto dto);
        Task DeleteLeaveAsync(int leaveId);
        Task<List<UserLeaveDto>> GetUserLeavesAsync(int userId);
    }
}
