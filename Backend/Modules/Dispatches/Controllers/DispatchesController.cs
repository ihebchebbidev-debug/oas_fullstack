using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApi.Modules.Dispatches.DTOs;
using MyApi.Modules.Dispatches.Services;
using MyApi.Modules.Shared.Services;

namespace MyApi.Modules.Dispatches.Controllers
{
    [ApiController]
    [Route("api/dispatches")]
    [Authorize]
    public class DispatchesController : ControllerBase
    {
        private readonly IDispatchService _service;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<DispatchesController> _logger;

        public DispatchesController(
            IDispatchService service, 
            ISystemLogService systemLogService,
            ILogger<DispatchesController> logger)
        {
            _service = service;
            _systemLogService = systemLogService;
            _logger = logger;
        }

        private string GetUserId() => User?.Identity?.Name ?? "system";
        private string GetUserName() => User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "system";

        [HttpPost("from-job/{jobId:int}")]
        public async Task<IActionResult> CreateFromJob(int jobId, [FromBody] CreateDispatchFromJobDto dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
            var userId = GetUserId();
            var result = await _service.CreateFromJobAsync(jobId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Dispatch created from job {jobId}", "Dispatches", "create", userId, GetUserName(), "Dispatch", result.Id.ToString());

            return CreatedAtAction(nameof(GetById), new { dispatchId = result.Id }, result);
        }

        [HttpPost("from-installation")]
        public async Task<IActionResult> CreateFromInstallation([FromBody] CreateDispatchFromInstallationDto dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
            var userId = GetUserId();
            var result = await _service.CreateFromInstallationAsync(dto, userId);

            await _systemLogService.LogSuccessAsync(
                $"Dispatch created from installation {dto.InstallationId} ({dto.JobIds.Count} jobs)", 
                "Dispatches", "create", userId, GetUserName(), "Dispatch", result.Id.ToString());

            return CreatedAtAction(nameof(GetById), new { dispatchId = result.Id }, result);
        }

        [HttpPost("from-service-order")]
        public async Task<IActionResult> CreateFromServiceOrder([FromBody] CreateDispatchFromServiceOrderDto dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
            var userId = GetUserId();
            var result = await _service.CreateFromServiceOrderAsync(dto, userId);

            await _systemLogService.LogSuccessAsync(
                $"Dispatch created from service order {dto.ServiceOrderId} ({result.JobIds.Count} jobs)",
                "Dispatches", "create", userId, GetUserName(), "Dispatch", result.Id.ToString());

            return CreatedAtAction(nameof(GetById), new { dispatchId = result.Id }, result);
        }

        // Find-or-create installation dispatch and append jobs to it.
        // Body uses CreateDispatchFromInstallationDto (jobIds, technicianIds, scheduledDate, ...).
        [HttpPost("installations/{installationId:int}/jobs")]
        public async Task<IActionResult> AddJobsToInstallationDispatch(int installationId, [FromBody] CreateDispatchFromInstallationDto dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
            if (dto.InstallationId == 0) dto.InstallationId = installationId;
            if (dto.InstallationId != installationId) return BadRequest(new { error = "installationId mismatch between route and body" });

            var userId = GetUserId();
            var result = await _service.AddJobsToInstallationDispatchAsync(
                installationId,
                dto.InstallationName,
                dto.JobIds,
                dto.AssignedTechnicianIds,
                dto.ScheduledDate,
                dto.ScheduledStartTime,
                dto.ScheduledEndTime,
                dto.Priority,
                dto.Notes,
                dto.SiteAddress,
                dto.ContactId,
                dto.ServiceOrderId,
                userId);

            await _systemLogService.LogSuccessAsync(
                $"Added {dto.JobIds.Count} job(s) to installation {installationId} dispatch (id {result.Id})",
                "Dispatches", "update", userId, GetUserName(), "Dispatch", result.Id.ToString());

            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DispatchQueryParams query)
        {
            var result = await _service.GetAllAsync(query);
            return Ok(result);
        }

        [HttpGet("{dispatchId:int}")]
        public async Task<IActionResult> GetById(int dispatchId)
        {
            var result = await _service.GetByIdAsync(dispatchId);
            return Ok(result);
        }

        [HttpPut("{dispatchId:int}")]
        public async Task<IActionResult> Update(int dispatchId, [FromBody] UpdateDispatchDto dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
            var userId = GetUserId();
            var result = await _service.UpdateAsync(dispatchId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Dispatch {dispatchId} updated", "Dispatches", "update", userId, GetUserName(), "Dispatch", dispatchId.ToString());

            return Ok(result);
        }

        [HttpPatch("{dispatchId:int}/status")]
        public async Task<IActionResult> UpdateStatus(int dispatchId, [FromBody] UpdateDispatchStatusDto dto)
        {
            var userId = GetUserId();
            var result = await _service.UpdateStatusAsync(dispatchId, dto, userId);

            await _systemLogService.LogInfoAsync($"Dispatch {dispatchId} status changed to {dto.Status}", "Dispatches", "update", userId, GetUserName(), "Dispatch", dispatchId.ToString());

            return Ok(result);
        }

        [HttpGet("{dispatchId:int}/audit-logs")]
        public async Task<IActionResult> GetAuditLogs(int dispatchId)
        {
            var logs = await _service.GetAuditLogsAsync(dispatchId);
            return Ok(logs);
        }

        [HttpPost("{dispatchId:int}/start")]
        public async Task<IActionResult> Start(int dispatchId, [FromBody] StartDispatchDto dto)
        {
            var userId = GetUserId();
            var result = await _service.StartDispatchAsync(dispatchId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Dispatch {dispatchId} started", "Dispatches", "update", userId, GetUserName(), "Dispatch", dispatchId.ToString());

            return Ok(result);
        }

        [HttpPost("{dispatchId:int}/complete")]
        public async Task<IActionResult> Complete(int dispatchId, [FromBody] CompleteDispatchDto dto)
        {
            var userId = GetUserId();
            var result = await _service.CompleteDispatchAsync(dispatchId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Dispatch {dispatchId} completed", "Dispatches", "update", userId, GetUserName(), "Dispatch", dispatchId.ToString());

            return Ok(result);
        }

        [HttpDelete("{dispatchId:int}")]
        public async Task<IActionResult> Delete(int dispatchId)
        {
            var userId = GetUserId();
            await _service.DeleteAsync(dispatchId, userId);

            await _systemLogService.LogSuccessAsync($"Dispatch {dispatchId} deleted", "Dispatches", "delete", userId, GetUserName(), "Dispatch", dispatchId.ToString());

            return NoContent();
        }

        [HttpPost("{dispatchId:int}/time-entries")]
        public async Task<IActionResult> AddTimeEntry(int dispatchId, [FromBody] CreateTimeEntryDto dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
            var userId = GetUserId();
            var result = await _service.AddTimeEntryAsync(dispatchId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Time entry added to dispatch {dispatchId}", "Dispatches", "create", userId, GetUserName(), "TimeEntry", result.Id.ToString());

            return CreatedAtAction(nameof(GetTimeEntries), new { dispatchId }, result);
        }

        [HttpGet("{dispatchId:int}/time-entries")]
        public async Task<IActionResult> GetTimeEntries(int dispatchId)
        {
            var result = await _service.GetTimeEntriesAsync(dispatchId);
            return Ok(new { data = result });
        }

        [HttpPost("{dispatchId:int}/time-entries/{timeEntryId:int}/approve")]
        public async Task<IActionResult> ApproveTimeEntry(int dispatchId, int timeEntryId, [FromBody] ApproveTimeEntryDto dto)
        {
            var userId = GetUserId();
            await _service.ApproveTimeEntryAsync(dispatchId, timeEntryId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Time entry {timeEntryId} approved for dispatch {dispatchId}", "Dispatches", "update", userId, GetUserName(), "TimeEntry", timeEntryId.ToString());

            return Ok(new { id = timeEntryId, status = "approved" });
        }

        [HttpPut("{dispatchId:int}/time-entries/{timeEntryId:int}")]
        public async Task<IActionResult> UpdateTimeEntry(int dispatchId, int timeEntryId, [FromBody] UpdateTimeEntryDto dto)
        {
            var userId = GetUserId();
            var result = await _service.UpdateTimeEntryAsync(dispatchId, timeEntryId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Time entry {timeEntryId} updated", "Dispatches", "update", userId, GetUserName(), "TimeEntry", timeEntryId.ToString());

            return Ok(new { data = result });
        }

        [HttpDelete("{dispatchId:int}/time-entries/{timeEntryId:int}")]
        public async Task<IActionResult> DeleteTimeEntry(int dispatchId, int timeEntryId)
        {
            var userId = GetUserId();
            await _service.DeleteTimeEntryAsync(dispatchId, timeEntryId, userId);

            await _systemLogService.LogSuccessAsync($"Time entry {timeEntryId} deleted from dispatch {dispatchId}", "Dispatches", "delete", userId, GetUserName(), "TimeEntry", timeEntryId.ToString());

            return NoContent();
        }

        [HttpPost("{dispatchId:int}/expenses")]
        public async Task<IActionResult> AddExpense(int dispatchId, [FromBody] CreateExpenseDto dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
            var userId = GetUserId();
            var result = await _service.AddExpenseAsync(dispatchId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Expense added to dispatch {dispatchId}: ${dto.Amount}", "Dispatches", "create", userId, GetUserName(), "Expense", result.Id.ToString());

            return CreatedAtAction(nameof(GetExpenses), new { dispatchId }, result);
        }

        [HttpGet("{dispatchId:int}/expenses")]
        public async Task<IActionResult> GetExpenses(int dispatchId)
        {
            var result = await _service.GetExpensesAsync(dispatchId);
            return Ok(new { data = result });
        }

        [HttpPost("{dispatchId:int}/expenses/{expenseId:int}/approve")]
        public async Task<IActionResult> ApproveExpense(int dispatchId, int expenseId, [FromBody] ApproveExpenseDto dto)
        {
            var userId = GetUserId();
            await _service.ApproveExpenseAsync(dispatchId, expenseId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Expense {expenseId} approved for dispatch {dispatchId}", "Dispatches", "update", userId, GetUserName(), "Expense", expenseId.ToString());

            return Ok(new { id = expenseId, status = "approved" });
        }

        [HttpPut("{dispatchId:int}/expenses/{expenseId:int}")]
        public async Task<IActionResult> UpdateExpense(int dispatchId, int expenseId, [FromBody] UpdateExpenseDto dto)
        {
            var userId = GetUserId();
            var result = await _service.UpdateExpenseAsync(dispatchId, expenseId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Expense {expenseId} updated", "Dispatches", "update", userId, GetUserName(), "Expense", expenseId.ToString());

            return Ok(new { data = result });
        }

        [HttpDelete("{dispatchId:int}/expenses/{expenseId:int}")]
        public async Task<IActionResult> DeleteExpense(int dispatchId, int expenseId)
        {
            var userId = GetUserId();
            await _service.DeleteExpenseAsync(dispatchId, expenseId, userId);

            await _systemLogService.LogSuccessAsync($"Expense {expenseId} deleted from dispatch {dispatchId}", "Dispatches", "delete", userId, GetUserName(), "Expense", expenseId.ToString());

            return NoContent();
        }

        [HttpPost("{dispatchId:int}/materials")]
        public async Task<IActionResult> AddMaterial(int dispatchId, [FromBody] CreateMaterialUsageDto dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
            var userId = GetUserId();
            var result = await _service.AddMaterialUsageAsync(dispatchId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Material added to dispatch {dispatchId}", "Dispatches", "create", userId, GetUserName(), "Material", result.Id.ToString());

            return CreatedAtAction(nameof(GetMaterials), new { dispatchId }, result);
        }

        [HttpGet("{dispatchId:int}/materials")]
        public async Task<IActionResult> GetMaterials(int dispatchId)
        {
            var result = await _service.GetMaterialsAsync(dispatchId);
            return Ok(new { data = result });
        }

        [HttpPost("{dispatchId:int}/materials/{materialId:int}/approve")]
        public async Task<IActionResult> ApproveMaterial(int dispatchId, int materialId, [FromBody] ApproveMaterialDto dto)
        {
            var userId = GetUserId();
            await _service.ApproveMaterialAsync(dispatchId, materialId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Material {materialId} approved for dispatch {dispatchId}", "Dispatches", "update", userId, GetUserName(), "Material", materialId.ToString());

            return Ok(new { id = materialId, status = "approved" });
        }

        [HttpPost("{dispatchId:int}/attachments")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAttachment(int dispatchId, [FromForm] AttachmentUploadDto dto)
        {
            var file = dto?.File;
            if (file == null) return BadRequest(new { error = "file is required" });
            var userId = GetUserId();
            var result = await _service.UploadAttachmentAsync(dispatchId, file, dto?.Category ?? "", dto?.Description, dto?.Latitude, dto?.Longitude, userId);

            await _systemLogService.LogSuccessAsync($"Attachment uploaded to dispatch {dispatchId}", "Dispatches", "create", userId, GetUserName(), "Attachment", result.Id.ToString());

            return CreatedAtAction(nameof(GetById), new { dispatchId }, result);
        }

        [HttpPost("{dispatchId:int}/notes")]
        public async Task<IActionResult> AddNote(int dispatchId, [FromBody] CreateNoteDto dto)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
            var userId = GetUserId();
            var result = await _service.AddNoteAsync(dispatchId, dto, userId);

            await _systemLogService.LogSuccessAsync($"Note added to dispatch {dispatchId}", "Dispatches", "create", userId, GetUserName(), "Note", result.Id.ToString());

            return CreatedAtAction(nameof(GetById), new { dispatchId }, result);
        }

        // Read notes for a dispatch. Used by the field-tech notes timeline.
        [HttpGet("{dispatchId:int}/notes")]
        public async Task<IActionResult> GetNotes(int dispatchId)
        {
            var notes = await _service.GetNotesAsync(dispatchId);
            return Ok(notes);
        }

        // Read activity/history log. Returns empty array until a real
        // DispatchHistory model exists. Avoids the offline hydration 404.
        [HttpGet("{dispatchId:int}/history")]
        public IActionResult GetActivityLog(int dispatchId)
        {
            return Ok(new object[0]);
        }

        // Append a fire-and-forget activity log entry. Currently a no-op so the
        // frontend's logActivity helper doesn't 405; persistence will land when
        // the DispatchHistory entity is introduced.
        [HttpPost("{dispatchId:int}/history")]
        public IActionResult LogActivity(int dispatchId, [FromBody] System.Text.Json.JsonElement _)
        {
            return NoContent();
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics([FromQuery] StatisticsQueryParams query)
        {
            var result = await _service.GetStatisticsAsync(query);
            return Ok(result);
        }
    }
}
