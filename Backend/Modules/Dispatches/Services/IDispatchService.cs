using System.Collections.Generic;
using System.Threading.Tasks;
using MyApi.Modules.Dispatches.DTOs;

namespace MyApi.Modules.Dispatches.Services
{
    public interface IDispatchService
    {
        Task<DispatchDto> CreateFromJobAsync(int jobId, CreateDispatchFromJobDto dto, string userId);
        Task<DispatchDto> CreateFromInstallationAsync(CreateDispatchFromInstallationDto dto, string userId);
        // Create a single dispatch holding ALL (or the supplied) jobs of a service order.
        // When dto.JobIds is empty, every non-deleted job on the service order is included.
        Task<DispatchDto> CreateFromServiceOrderAsync(CreateDispatchFromServiceOrderDto dto, string userId);
        // Find an existing installation dispatch on the given date for the given technicians and append jobs to it,
        // or create a new installation dispatch when none exists. Used by both REST endpoint and PlanningService.
        Task<DispatchDto> AddJobsToInstallationDispatchAsync(int installationId, string installationName, System.Collections.Generic.List<int> jobIds, System.Collections.Generic.List<string> technicianIds, System.DateTime scheduledDate, System.TimeSpan? scheduledStartTime, System.TimeSpan? scheduledEndTime, string priority, string? notes, string? siteAddress, int? contactId, int? serviceOrderId, string userId);
        Task<PagedResult<DispatchListItemDto>> GetAllAsync(DispatchQueryParams query);
        Task<DispatchDto> GetByIdAsync(int dispatchId);
        Task<DispatchDto> UpdateAsync(int dispatchId, UpdateDispatchDto dto, string userId);
        Task<DispatchDto> UpdateStatusAsync(int dispatchId, UpdateDispatchStatusDto dto, string userId);
        Task<System.Collections.Generic.List<MyApi.Modules.Dispatches.Models.DispatchAuditLog>> GetAuditLogsAsync(int dispatchId);
        Task<DispatchDto> StartDispatchAsync(int dispatchId, StartDispatchDto dto, string userId);
        Task<DispatchDto> CompleteDispatchAsync(int dispatchId, CompleteDispatchDto dto, string userId);
        Task DeleteAsync(int dispatchId, string userId);

        // Time entries
        Task<TimeEntryDto> AddTimeEntryAsync(int dispatchId, CreateTimeEntryDto dto, string userId);
        Task<IEnumerable<TimeEntryDto>> GetTimeEntriesAsync(int dispatchId);
        Task<TimeEntryDto> UpdateTimeEntryAsync(int dispatchId, int timeEntryId, UpdateTimeEntryDto dto, string userId);
        Task DeleteTimeEntryAsync(int dispatchId, int timeEntryId, string userId);
        Task ApproveTimeEntryAsync(int dispatchId, int timeEntryId, ApproveTimeEntryDto dto, string userId);

        // Expenses
        Task<ExpenseDto> AddExpenseAsync(int dispatchId, CreateExpenseDto dto, string userId);
        Task<IEnumerable<ExpenseDto>> GetExpensesAsync(int dispatchId);
        Task<ExpenseDto> UpdateExpenseAsync(int dispatchId, int expenseId, UpdateExpenseDto dto, string userId);
        Task DeleteExpenseAsync(int dispatchId, int expenseId, string userId);
        Task ApproveExpenseAsync(int dispatchId, int expenseId, ApproveExpenseDto dto, string userId);

        // Materials
        Task<MaterialDto> AddMaterialUsageAsync(int dispatchId, CreateMaterialUsageDto dto, string userId);
        Task<IEnumerable<MaterialDto>> GetMaterialsAsync(int dispatchId);
        Task ApproveMaterialAsync(int dispatchId, int materialId, ApproveMaterialDto dto, string userId);

        // Attachments & Notes
        Task<AttachmentUploadResponseDto> UploadAttachmentAsync(int dispatchId, Microsoft.AspNetCore.Http.IFormFile file, string category, string? description, double? latitude, double? longitude, string userId);
        Task<NoteDto> AddNoteAsync(int dispatchId, CreateNoteDto dto, string userId);
        Task<System.Collections.Generic.List<NoteDto>> GetNotesAsync(int dispatchId);

        // Statistics
        Task<DispatchStatisticsDto> GetStatisticsAsync(StatisticsQueryParams query);
    }
}
