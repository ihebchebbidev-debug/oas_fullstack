using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Data;
using MyApi.Modules.ServiceOrders.DTOs;
using MyApi.Modules.ServiceOrders.Models;
using MyApi.Modules.ServiceOrders.Services;
using MyApi.Modules.Shared.Services;
using System.Security.Claims;

namespace MyApi.Modules.ServiceOrders.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/service-orders")]
    public class ServiceOrdersController : ControllerBase
    {
        private readonly IServiceOrderService _serviceOrderService;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<ServiceOrdersController> _logger;
        private readonly ApplicationDbContext _context;

        public ServiceOrdersController(
            IServiceOrderService serviceOrderService, 
            ISystemLogService systemLogService,
            ILogger<ServiceOrdersController> logger,
            ApplicationDbContext context)
        {
            _serviceOrderService = serviceOrderService;
            _systemLogService = systemLogService;
            _logger = logger;
            _context = context;
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        }

        private string GetCurrentUserName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst("FirstName")?.Value + " " + User.FindFirst("LastName")?.Value ?? 
                   User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   "anonymous";
        }

        [HttpPost("from-sale/{saleId:int}")]
        public async Task<IActionResult> CreateFromSale(int saleId, [FromBody] CreateServiceOrderDto createDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.CreateFromSaleAsync(saleId, createDto, userId);

                await _systemLogService.LogSuccessAsync($"Service order created from sale {saleId}: {serviceOrder.OrderNumber}", "ServiceOrders", "create", userId, GetCurrentUserName(), "ServiceOrder", serviceOrder.Id.ToString());

                return CreatedAtAction(nameof(GetServiceOrderById), new { id = serviceOrder.Id }, new
                {
                    success = true,
                    data = serviceOrder
                });
            }
            catch (KeyNotFoundException ex)
            {
                await _systemLogService.LogWarningAsync($"Failed to create service order from sale {saleId}: {ex.Message}", "ServiceOrders", "create", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", details: ex.Message);
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            catch (InvalidOperationException ex)
            {
                await _systemLogService.LogWarningAsync($"Conflict creating service order from sale {saleId}: {ex.Message}", "ServiceOrders", "create", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", details: ex.Message);
                return Conflict(new { success = false, error = new { code = "CONFLICT", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service order from sale {SaleId}", saleId);
                await _systemLogService.LogErrorAsync($"Failed to create service order from sale {saleId}", "ServiceOrders", "create", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// POST /api/service-orders/direct — create a Service Order without an
        /// originating Offer or Sale. Only ContactId is required.
        /// </summary>
        [HttpPost("direct")]
        public async Task<IActionResult> CreateDirect([FromBody] CreateDirectServiceOrderDto createDto)
        {
            if (createDto == null)
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = "Body is required" } });
            if (createDto.ContactId <= 0)
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = "ContactId is required" } });

            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.CreateDirectAsync(createDto, userId);

                await _systemLogService.LogSuccessAsync(
                    $"Direct service order created: {serviceOrder.OrderNumber}",
                    "ServiceOrders", "create", userId, GetCurrentUserName(),
                    "ServiceOrder", serviceOrder.Id.ToString());

                return CreatedAtAction(nameof(GetServiceOrderById), new { id = serviceOrder.Id }, new
                {
                    success = true,
                    data = serviceOrder
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating direct service order");
                await _systemLogService.LogErrorAsync("Failed to create direct service order",
                    "ServiceOrders", "create", GetCurrentUserId(), GetCurrentUserName(),
                    "ServiceOrder", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceOrders(
            [FromQuery] string? status = null,
            [FromQuery] string? priority = null,
            [FromQuery] int? contact_id = null,
            [FromQuery] int? sale_id = null,
            [FromQuery] DateTime? start_date = null,
            [FromQuery] DateTime? end_date = null,
            [FromQuery] string? payment_status = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string sortBy = "created_at",
            [FromQuery] string sortOrder = "desc"
        )
        {
            try
            {
                var result = await _serviceOrderService.GetServiceOrdersAsync(
                    status, priority, contact_id, sale_id, start_date, end_date, payment_status, search,
                    page, pageSize, sortBy, sortOrder
                );
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching service orders");
                await _systemLogService.LogErrorAsync("Failed to retrieve service orders", "ServiceOrders", "read", GetCurrentUserId(), GetCurrentUserName(), details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetServiceOrderById(int id, [FromQuery] bool includeJobs = true)
        {
            try
            {
                var serviceOrder = await _serviceOrderService.GetServiceOrderByIdAsync(id, includeJobs);
                if (serviceOrder == null)
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });

                return Ok(new { success = true, data = serviceOrder });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching service order {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>GET /api/service-orders/{serviceOrderId}/jobs/{jobId} — aligned with client serviceOrdersApi.getJobById</summary>
        [HttpGet("{serviceOrderId:int}/jobs/{jobId:int}")]
        public async Task<IActionResult> GetServiceOrderJob(int serviceOrderId, int jobId)
        {
            try
            {
                var job = await _serviceOrderService.GetServiceOrderJobAsync(serviceOrderId, jobId);
                if (job == null)
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Job not found" } });
                return Ok(new { success = true, data = job });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching job {JobId} for service order {ServiceOrderId}", jobId, serviceOrderId);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>POST /api/service-orders/{serviceOrderId}/jobs — add a new job to an existing service order (aligned with client serviceOrdersApi.createJob).</summary>
        [HttpPost("{serviceOrderId:int}/jobs")]
        public async Task<IActionResult> CreateServiceOrderJob(int serviceOrderId, [FromBody] CreateServiceOrderJobDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = "Title is required" } });
            try
            {
                var userId = GetCurrentUserId();
                var job = await _serviceOrderService.CreateServiceOrderJobAsync(serviceOrderId, dto, userId);
                await _systemLogService.LogSuccessAsync(
                    $"Job '{job.Title}' added to service order {serviceOrderId}",
                    "ServiceOrders", "create", userId, GetCurrentUserName(),
                    "ServiceOrderJob", job.Id.ToString());
                return CreatedAtAction(
                    nameof(GetServiceOrderJob),
                    new { serviceOrderId, jobId = job.Id },
                    new { success = true, data = job });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating job for service order {ServiceOrderId}", serviceOrderId);
                try
                {
                    await _systemLogService.LogErrorAsync(
                        $"[500] POST /api/service-orders/{serviceOrderId}/jobs: {ex.GetType().Name}: {ex.Message}",
                        "ServiceOrders", "create", null, null, "ServiceOrderJob", null,
                        (ex.InnerException?.ToString() ?? ex.ToString()).Substring(0, Math.Min(3000, (ex.InnerException?.ToString() ?? ex.ToString()).Length)));
                }
                catch { }
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }


        /// <summary>PATCH /api/service-orders/{serviceOrderId}/jobs/{jobId}/status — aligned with client updateJobStatus (primary)</summary>
        [HttpPatch("{serviceOrderId:int}/jobs/{jobId:int}/status")]
        public async Task<IActionResult> PatchServiceOrderJobStatus(int serviceOrderId, int jobId, [FromBody] UpdateServiceOrderJobStatusDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = "Status is required" } });
            try
            {
                var userId = GetCurrentUserId();
                var job = await _serviceOrderService.PatchServiceOrderJobStatusAsync(serviceOrderId, jobId, dto, userId);
                return Ok(new { success = true, data = job });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Job not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error patching job status {JobId} for service order {ServiceOrderId}", jobId, serviceOrderId);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>PUT /api/service-orders/{serviceOrderId}/jobs/{jobId} — aligned with client updateJobStatus fallback</summary>
        [HttpPut("{serviceOrderId:int}/jobs/{jobId:int}")]
        public async Task<IActionResult> UpdateServiceOrderJob(int serviceOrderId, int jobId, [FromBody] UpdateServiceOrderJobDto dto)
        {
            if (dto == null)
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = "Body is required" } });
            try
            {
                var userId = GetCurrentUserId();
                var job = await _serviceOrderService.UpdateServiceOrderJobAsync(serviceOrderId, jobId, dto, userId);
                return Ok(new { success = true, data = job });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Job not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating job {JobId} for service order {ServiceOrderId}", jobId, serviceOrderId);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateServiceOrder(int id, [FromBody] UpdateServiceOrderDto updateDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.UpdateServiceOrderAsync(id, updateDto, userId);

                await _systemLogService.LogSuccessAsync($"Service order updated: {serviceOrder.OrderNumber}", "ServiceOrders", "update", userId, GetCurrentUserName(), "ServiceOrder", id.ToString());

                return Ok(new { success = true, data = serviceOrder });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to update service order {id}", "ServiceOrders", "update", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> PatchServiceOrder(int id, [FromBody] UpdateServiceOrderDto updateDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.PatchServiceOrderAsync(id, updateDto, userId);

                await _systemLogService.LogSuccessAsync($"Service order patched: {serviceOrder.OrderNumber}", "ServiceOrders", "update", userId, GetCurrentUserName(), "ServiceOrder", id.ToString());

                return Ok(new { success = true, data = serviceOrder });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error patching service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to patch service order {id}", "ServiceOrders", "update", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateServiceOrderStatusDto statusDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.UpdateStatusAsync(id, statusDto, userId);

                await _systemLogService.LogInfoAsync($"Service order {id} status changed to {statusDto.Status}", "ServiceOrders", "update", userId, GetCurrentUserName(), "ServiceOrder", id.ToString());

                return Ok(new { success = true, data = serviceOrder });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (InvalidOperationException ex)
            {
                await _systemLogService.LogWarningAsync($"Invalid status transition for service order {id}: {ex.Message}", "ServiceOrders", "update", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString(), ex.Message);
                return BadRequest(new { success = false, error = new { code = "INVALID_STATUS_TRANSITION", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to update status for service order {id}", "ServiceOrders", "update", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>POST /api/service-orders/{id}/recalculate-status — server-side reconcile of SO status from its dispatches.</summary>
        [HttpPost("{id:int}/recalculate-status")]
        public async Task<IActionResult> RecalculateStatus(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.RecalculateStatusFromDispatchesAsync(id, userId);
                return Ok(new { success = true, data = serviceOrder });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating status for service order {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/approve")]
        public async Task<IActionResult> Approve(int id, [FromBody] ApproveServiceOrderDto approveDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.ApproveAsync(id, approveDto, userId);

                await _systemLogService.LogSuccessAsync($"Service order {id} approved", "ServiceOrders", "update", userId, GetCurrentUserName(), "ServiceOrder", id.ToString());

                return Ok(new { success = true, data = serviceOrder });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to approve service order {id}", "ServiceOrders", "update", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/complete")]
        public async Task<IActionResult> Complete(int id, [FromBody] CompleteServiceOrderDto completeDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.CompleteAsync(id, completeDto, userId);

                await _systemLogService.LogSuccessAsync($"Service order {id} completed", "ServiceOrders", "update", userId, GetCurrentUserName(), "ServiceOrder", id.ToString());

                return Ok(new { success = true, data = serviceOrder });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to complete service order {id}", "ServiceOrders", "update", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// Fix §4.2: retry shadow-Sale generation after CompleteAsync returned a
        /// `shadow_sale_failed` warning. Safe to call multiple times.
        /// </summary>
        [HttpPost("{id:int}/retry-shadow-sale")]
        public async Task<IActionResult> RetryShadowSale(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.RetryShadowSaleAsync(id, userId);
                await _systemLogService.LogSuccessAsync($"Shadow Sale retried for service order {id}", "ServiceOrders", "update", userId, GetCurrentUserName(), "ServiceOrder", id.ToString());
                return Ok(new { success = true, data = serviceOrder });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrying shadow Sale for service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to retry shadow Sale for service order {id}", "ServiceOrders", "update", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelServiceOrderDto cancelDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.CancelAsync(id, cancelDto, userId);

                await _systemLogService.LogWarningAsync($"Service order {id} cancelled: {cancelDto.CancellationReason}", "ServiceOrders", "update", userId, GetCurrentUserName(), "ServiceOrder", id.ToString());

                return Ok(new { success = true, data = serviceOrder });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to cancel service order {id}", "ServiceOrders", "update", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _serviceOrderService.DeleteAsync(id, userId);
                if (!result)
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });

                await _systemLogService.LogSuccessAsync($"Service order deleted: ID {id}", "ServiceOrders", "delete", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString());

                return Ok(new { success = true, message = "Service order deleted successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to delete service order {id}", "ServiceOrders", "delete", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? status = null,
            [FromQuery] int? contactId = null
        )
        {
            try
            {
                var stats = await _serviceOrderService.GetStatisticsAsync(startDate, endDate, status, contactId);
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching service order statistics");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // ============== AGGREGATION ENDPOINTS ==============

        [HttpGet("{id:int}/dispatches")]
        public async Task<IActionResult> GetDispatches(int id)
        {
            try
            {
                var dispatches = await _serviceOrderService.GetDispatchesForServiceOrderAsync(id);
                return Ok(new { success = true, data = dispatches });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dispatches for service order {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("{id:int}/time-entries")]
        public async Task<IActionResult> GetTimeEntries(int id)
        {
            try
            {
                var entries = await _serviceOrderService.GetTimeEntriesForServiceOrderAsync(id);
                return Ok(new { success = true, data = entries });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching time entries for service order {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("{id:int}/expenses")]
        public async Task<IActionResult> GetExpenses(int id)
        {
            try
            {
                var expenses = await _serviceOrderService.GetExpensesForServiceOrderAsync(id);
                return Ok(new { success = true, data = expenses });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching expenses for service order {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("{id:int}/materials")]
        public async Task<IActionResult> GetMaterials(int id)
        {
            try
            {
                var materials = await _serviceOrderService.GetMaterialsForServiceOrderAsync(id);
                return Ok(new { success = true, data = materials });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching materials for service order {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/materials")]
        public async Task<IActionResult> AddMaterial(int id, [FromBody] CreateServiceOrderMaterialDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var material = await _serviceOrderService.AddMaterialAsync(id, dto, userId);

                await _systemLogService.LogSuccessAsync($"Material added to service order {id}", "ServiceOrders", "create", userId, GetCurrentUserName(), "ServiceOrderMaterial", material.Id.ToString());

                return CreatedAtAction(nameof(GetMaterials), new { id }, new { success = true, data = material });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding material to service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to add material to service order {id}", "ServiceOrders", "create", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrderMaterial", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPut("{id:int}/materials/{materialId:int}")]
        public async Task<IActionResult> UpdateMaterial(int id, int materialId, [FromBody] UpdateServiceOrderMaterialDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var material = await _serviceOrderService.UpdateMaterialAsync(id, materialId, dto, userId);
                if (material == null)
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Material not found" } });

                await _systemLogService.LogSuccessAsync($"Material {materialId} updated in service order {id}", "ServiceOrders", "update", userId, GetCurrentUserName(), "ServiceOrderMaterial", materialId.ToString());

                return Ok(new { success = true, data = material });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating material {MaterialId} for service order {Id}", materialId, id);
                await _systemLogService.LogErrorAsync($"Failed to update material {materialId} in service order {id}", "ServiceOrders", "update", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrderMaterial", materialId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpDelete("{id:int}/materials/{materialId:int}")]
        public async Task<IActionResult> DeleteMaterial(int id, int materialId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _serviceOrderService.DeleteMaterialAsync(id, materialId, userId);
                if (!result)
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Material not found" } });

                await _systemLogService.LogSuccessAsync($"Material {materialId} deleted from service order {id}", "ServiceOrders", "delete", userId, GetCurrentUserName(), "ServiceOrderMaterial", materialId.ToString());

                return Ok(new { success = true, message = "Material deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting material {MaterialId} from service order {Id}", materialId, id);
                await _systemLogService.LogErrorAsync($"Failed to delete material {materialId} from service order {id}", "ServiceOrders", "delete", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrderMaterial", materialId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("{id:int}/notes")]
        public async Task<IActionResult> GetNotes(int id)
        {
            try
            {
                var notes = await _serviceOrderService.GetNotesForServiceOrderAsync(id);
                return Ok(new { success = true, data = notes });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching notes for service order {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/notes")]
        public async Task<IActionResult> AddNote(int id, [FromBody] CreateServiceOrderNoteDto noteDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userName = GetCurrentUserName();
                
                // Verify service order exists
                var serviceOrder = await _serviceOrderService.GetServiceOrderByIdAsync(id, false);
                if (serviceOrder == null)
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });

                // Defensively cap Type to the column width (varchar(50)) so an
                // over-length activity category can never throw a 22001 again.
                var noteType = string.IsNullOrWhiteSpace(noteDto.Type) ? "internal" : noteDto.Type.Trim();
                if (noteType.Length > 50) noteType = noteType.Substring(0, 50);

                var note = new ServiceOrderNote
                {
                    ServiceOrderId = id,
                    Content = noteDto.Content,
                    Type = noteType,
                    CreatedBy = userId,
                    CreatedByName = userName,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ServiceOrderNotes.Add(note);
                await _context.SaveChangesAsync();

                await _systemLogService.LogSuccessAsync($"Note added to service order {id}", "ServiceOrders", "create", userId, userName, "ServiceOrderNote", note.Id.ToString());

                return Created($"/api/service-orders/{id}/notes/{note.Id}", new { 
                    success = true, 
                    data = new ServiceOrderNoteDto
                    {
                        Id = note.Id,
                        ServiceOrderId = note.ServiceOrderId,
                        Content = note.Content,
                        Type = note.Type,
                        CreatedBy = note.CreatedBy,
                        CreatedByName = note.CreatedByName,
                        CreatedAt = note.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding note to service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to add note to service order {id}", "ServiceOrders", "create", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrderNote", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("{id:int}/full-summary")]
        public async Task<IActionResult> GetFullSummary(int id)
        {
            try
            {
                var summary = await _serviceOrderService.GetFullSummaryAsync(id);
                return Ok(new { success = true, data = summary });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching full summary for service order {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // ========== TIME ENTRY ENDPOINTS ==========

        [HttpPost("{id:int}/time-entries")]
        public async Task<IActionResult> AddTimeEntry(int id, [FromBody] CreateServiceOrderTimeEntryDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var timeEntry = await _serviceOrderService.AddTimeEntryAsync(id, dto, userId);

                await _systemLogService.LogSuccessAsync($"Time entry added to service order {id}", "ServiceOrders", "create", userId, GetCurrentUserName(), "TimeEntry", timeEntry.Id.ToString());

                return CreatedAtAction(nameof(GetTimeEntries), new { id }, new { success = true, data = timeEntry });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding time entry to service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to add time entry to service order {id}", "ServiceOrders", "create", GetCurrentUserId(), GetCurrentUserName(), "TimeEntry", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpDelete("{id:int}/time-entries/{timeEntryId:int}")]
        public async Task<IActionResult> DeleteTimeEntry(int id, int timeEntryId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _serviceOrderService.DeleteTimeEntryAsync(id, timeEntryId, userId);
                if (!result)
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Time entry not found" } });

                await _systemLogService.LogSuccessAsync($"Time entry {timeEntryId} deleted from service order {id}", "ServiceOrders", "delete", userId, GetCurrentUserName(), "TimeEntry", timeEntryId.ToString());

                return Ok(new { success = true, message = "Time entry deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting time entry {TimeEntryId} from service order {Id}", timeEntryId, id);
                await _systemLogService.LogErrorAsync($"Failed to delete time entry {timeEntryId} from service order {id}", "ServiceOrders", "delete", GetCurrentUserId(), GetCurrentUserName(), "TimeEntry", timeEntryId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // ========== EXPENSE ENDPOINTS ==========

        [HttpPost("{id:int}/expenses")]
        public async Task<IActionResult> AddExpense(int id, [FromBody] CreateServiceOrderExpenseDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var expense = await _serviceOrderService.AddExpenseAsync(id, dto, userId);

                await _systemLogService.LogSuccessAsync($"Expense added to service order {id}: ${dto.Amount}", "ServiceOrders", "create", userId, GetCurrentUserName(), "Expense", expense.Id.ToString());

                return CreatedAtAction(nameof(GetExpenses), new { id }, new { success = true, data = expense });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding expense to service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to add expense to service order {id}", "ServiceOrders", "create", GetCurrentUserId(), GetCurrentUserName(), "Expense", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpDelete("{id:int}/expenses/{expenseId:int}")]
        public async Task<IActionResult> DeleteExpense(int id, int expenseId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _serviceOrderService.DeleteExpenseAsync(id, expenseId, userId);
                if (!result)
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Expense not found" } });

                await _systemLogService.LogSuccessAsync($"Expense {expenseId} deleted from service order {id}", "ServiceOrders", "delete", userId, GetCurrentUserName(), "Expense", expenseId.ToString());

                return Ok(new { success = true, message = "Expense deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expense {ExpenseId} from service order {Id}", expenseId, id);
                await _systemLogService.LogErrorAsync($"Failed to delete expense {expenseId} from service order {id}", "ServiceOrders", "delete", GetCurrentUserId(), GetCurrentUserName(), "Expense", expenseId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // ========== INVOICE PREPARATION ==========

        [HttpPost("{id:int}/prepare-invoice")]
        public async Task<IActionResult> PrepareForInvoice(int id, [FromBody] PrepareInvoiceDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var serviceOrder = await _serviceOrderService.PrepareForInvoiceAsync(id, dto, userId);

                await _systemLogService.LogSuccessAsync($"Invoice prepared for service order {id}", "ServiceOrders", "update", userId, GetCurrentUserName(), "ServiceOrder", id.ToString());

                return Ok(new { success = true, data = serviceOrder });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Service order not found" } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing invoice for service order {Id}", id);
                await _systemLogService.LogErrorAsync($"Failed to prepare invoice for service order {id}", "ServiceOrders", "update", GetCurrentUserId(), GetCurrentUserName(), "ServiceOrder", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }
    }
}
