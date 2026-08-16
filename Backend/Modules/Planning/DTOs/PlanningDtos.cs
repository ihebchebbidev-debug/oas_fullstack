using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Planning.DTOs
{
    // Assignment DTOs
    public class AssignJobDto
    {
        [Required]
        public int JobId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one technician must be assigned")]
        public List<string> TechnicianIds { get; set; } = new();

        [Required]
        public DateTime ScheduledDate { get; set; }

        [Required]
        public TimeSpan ScheduledStartTime { get; set; }

        [Required]
        public TimeSpan ScheduledEndTime { get; set; }

        public string Priority { get; set; } = "medium";

        public string? Notes { get; set; }

        public bool AutoCreateDispatch { get; set; } = true;

        /// <summary>If true, skip hard-fail on overlapping dispatches (overlaps still reported as warnings).</summary>
        public bool AllowOverlap { get; set; } = false;
    }

    public class AssignJobResponseDto
    {
        public ServiceOrderJobDto Job { get; set; } = null!;
        public object? Dispatch { get; set; }
    }

    // Batch Assignment DTOs
    public class BatchAssignDto
    {
        [Required]
        [MinLength(1)]
        public List<AssignJobDto> Assignments { get; set; } = new();

        public bool AutoCreateDispatches { get; set; } = true;
    }

    public class BatchAssignResponseDto
    {
        public int Successful { get; set; }
        public int Failed { get; set; }
        public List<BatchAssignResult> Results { get; set; } = new();
    }

    public class BatchAssignResult
    {
        public int JobId { get; set; }
        public string Status { get; set; } = null!; // "success" or "failed"
        public int? DispatchId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // Validation DTOs
    public class ValidateAssignmentDto
    {
        public int JobId { get; set; }
        public List<string> TechnicianIds { get; set; } = new();
        public DateTime ScheduledDate { get; set; }
        public TimeSpan ScheduledStartTime { get; set; }
        public TimeSpan ScheduledEndTime { get; set; }
        public bool AllowOverlap { get; set; } = false;
    }

    public class AssignmentValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<AssignmentConflict> Conflicts { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class AssignmentConflict
    {
        public string Type { get; set; } = null!;
        public string Message { get; set; } = null!;
        public object? ConflictingData { get; set; }
    }

    // Schedule DTOs
    public class UserScheduleDto
    {
        public string UserId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public Dictionary<string, WorkingHoursDto?> WorkingHours { get; set; } = new();
        public List<DispatchScheduleDto> Dispatches { get; set; } = new();
        public List<UserLeaveDto> Leaves { get; set; } = new();
        public decimal TotalScheduledHours { get; set; }
        public decimal AvailableHours { get; set; }
    }

    public class WorkingHoursDto
    {
        public string Start { get; set; } = null!;
        public string End { get; set; } = null!;
    }

    public class DispatchScheduleDto
    {
        public int Id { get; set; }
        public string DispatchNumber { get; set; } = null!;
        public int? JobId { get; set; }
        public string JobTitle { get; set; } = null!;
        public int? ServiceOrderId { get; set; }
        public DateTime ScheduledDate { get; set; }
        public TimeSpan ScheduledStartTime { get; set; }
        public TimeSpan ScheduledEndTime { get; set; }
        public int EstimatedDuration { get; set; }
        public string Status { get; set; } = null!;
        public string Priority { get; set; } = null!;
        public LocationDto? Location { get; set; }
    }

    public class LocationDto
    {
        public string Address { get; set; } = null!;
        public double? Lat { get; set; }
        public double? Lng { get; set; }
    }

    public class UserLeaveDto
    {
        public int Id { get; set; }
        public string LeaveType { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = null!;
        public string? Reason { get; set; }
    }

    // Create/Update Working Hours DTOs
    public class CreateWorkingHoursDto
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public Dictionary<int, DayScheduleDto> DaySchedules { get; set; } = new();
    }

    public class DayScheduleDto
    {
        public bool Enabled { get; set; } = true;
        public string StartTime { get; set; } = "08:00";
        public string EndTime { get; set; } = "17:00";
        public string? LunchStart { get; set; }
        public string? LunchEnd { get; set; }
        public bool FullDayOff { get; set; } = false;
    }

    public class UpdateUserScheduleDto
    {
        [Required]
        public int UserId { get; set; }
        
        public Dictionary<int, DayScheduleDto>? DaySchedules { get; set; }
        public string? Status { get; set; }
        public string? ScheduleNote { get; set; }
    }

    // Create Leave DTO
    public class CreateLeaveDto
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public string LeaveType { get; set; } = null!;
        
        [Required]
        public DateTime StartDate { get; set; }
        
        [Required]
        public DateTime EndDate { get; set; }
        
        public string? Reason { get; set; }

        /// <summary>Optional initial status — defaults to "pending" (awaiting approval).</summary>
        public string? Status { get; set; }
    }

    public class UpdateLeaveDto
    {
        public string? LeaveType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Reason { get; set; }
        public string? Status { get; set; }
    }

    // Full user schedule response
    public class UserFullScheduleDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string? Status { get; set; }
        public string? ScheduleNote { get; set; }
        public Dictionary<int, DayScheduleDto> DaySchedules { get; set; } = new();
        public List<UserLeaveDto> Leaves { get; set; } = new();
    }

    // Service Order Job DTO - gets customer info from ServiceOrder -> Contact join
    public class ServiceOrderJobDto
    {
        public int Id { get; set; }
        public int ServiceOrderId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string Status { get; set; } = null!;
        public string Priority { get; set; } = null!;
        public int? EstimatedDuration { get; set; }
        public List<string>? RequiredSkills { get; set; }
        public List<string> AssignedTechnicianIds { get; set; } = new();
        public DateTime? ScheduledDate { get; set; }
        public LocationDto? Location { get; set; }
        // Customer info comes from ServiceOrder -> Contact navigation
        public ContactInfoDto? Contact { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Contact info DTO for jobs - populated from ServiceOrder.Contact navigation
    public class ContactInfoDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Company { get; set; }
    }

    // User Availability DTO
    public class UserAvailabilityDto
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public List<string> Skills { get; set; } = new();
        public string Status { get; set; } = null!;
        public bool IsAvailable { get; set; }
        public int AvailableMinutes { get; set; }
        public int ScheduledMinutes { get; set; }
        public decimal UtilizationPercentage { get; set; }
    }

    // Paged result for jobs
    public class PagedResult<T>
    {
        public List<T> Data { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}
