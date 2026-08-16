using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Infrastructure;
using MyApi.Modules.Purchases.DTOs;
using MyApi.Modules.Purchases.Services;
using MyApi.Modules.RetenueSource.Services;
using MyApi.Modules.Shared.Services;
using System.Security.Claims;

namespace MyApi.Modules.Purchases.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/supplier-invoices")]
    public class SupplierInvoicesController : ControllerBase
    {
        private readonly ISupplierInvoiceService _service;
        private readonly ISystemLogService _systemLogService;
        private readonly IRSService _rsService;
        private readonly ILogger<SupplierInvoicesController> _logger;

        public SupplierInvoicesController(ISupplierInvoiceService service, ISystemLogService systemLogService, IRSService rsService, ILogger<SupplierInvoicesController> logger)
        {
            _service = service;
            _systemLogService = systemLogService;
            _rsService = rsService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/supplier-invoices/{id}/tej-xml — download the TEJ/RiTEJ XML for this
        /// invoice on demand. Returns 400 with a `missing` list if info still needs filling.
        /// </summary>
        [RequirePermission("purchases", "read")]
        [HttpGet("{id:int}/tej-xml")]
        public async Task<IActionResult> DownloadTejXml(int id)
        {
            try
            {
                var result = await _rsService.BuildTejXmlForSupplierInvoiceAsync(id, GetUserId());
                if (!result.Ok)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = new
                        {
                            code = "TEJ_INCOMPLETE",
                            message = "Some information required for the TEJ XML is missing. Please complete it and try again.",
                            missing = result.Missing
                        }
                    });
                }
                var bytes = System.Text.Encoding.UTF8.GetBytes(result.Xml!);
                return File(bytes, "application/xml", result.FileName);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Invoice not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating TEJ XML for supplier invoice {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        private string GetUserName() => User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value ?? "anonymous";

        [RequirePermission("purchases", "read")]
        [HttpGet]
        public async Task<IActionResult> GetInvoices(
            [FromQuery] string? status = null, [FromQuery] string? supplier_id = null,
            [FromQuery] bool? rs_applicable = null, [FromQuery] DateTime? date_from = null,
            [FromQuery] DateTime? date_to = null, [FromQuery] string? search = null,
            [FromQuery] int page = 1, [FromQuery] int limit = 20,
            [FromQuery] string sort_by = "created_date", [FromQuery] string sort_order = "desc",
            [FromQuery] bool? overdue_only = null)
        {
            try
            {
                var result = await _service.GetInvoicesAsync(status, supplier_id, rs_applicable, date_from, date_to, search, page, limit, sort_by, sort_order, overdue_only);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching supplier invoices");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "read")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetInvoice(int id)
        {
            try
            {
                var invoice = await _service.GetInvoiceByIdAsync(id);
                if (invoice == null) return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Invoice not found" } });
                return Ok(new { success = true, data = invoice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching supplier invoice {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "create")]
        [HttpPost]
        public async Task<IActionResult> CreateInvoice(
            [FromBody] CreateSupplierInvoiceDto dto,
            // Idempotency token — retried POST with the same value returns the
            // existing invoice instead of double-booking a financial document.
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
        {
            try
            {
                var userId = GetUserId();
                var invoice = await _service.CreateInvoiceAsync(dto, userId, GetUserName(), idempotencyKey);
                await _systemLogService.LogSuccessAsync($"Supplier invoice created: {invoice.InvoiceNumber}", "Purchases", "create", userId, GetUserName(), "SupplierInvoice", invoice.Id.ToString());
                return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, new { success = true, data = invoice });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            // Cross-supplier mismatch / orphan PO-item linkage / missing PO ref /
            // duplicate supplier-ref (natural-key idempotency) / negative qty etc.
            // The service prefixes the message with a bracketed code so the FE can
            // branch on it. Duplicate supplier-ref becomes 409 Conflict; the rest 400.
            catch (InvalidOperationException ex)
            {
                if (ex.Message.StartsWith("[DUPLICATE_SUPPLIER_REF]", StringComparison.Ordinal))
                    return Conflict(new { success = false, error = new { code = "DUPLICATE_SUPPLIER_REF", message = ex.Message } });
                return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating supplier invoice");
                await _systemLogService.LogErrorAsync("Failed to create supplier invoice", "Purchases", "create", GetUserId(), GetUserName(), details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }


        [RequirePermission("purchases", "update")]
        [HttpPatch("{id:int}")]
        public async Task<IActionResult> UpdateInvoice(int id, [FromBody] UpdateSupplierInvoiceDto dto)
        {
            try
            {
                var userId = GetUserId();
                var invoice = await _service.UpdateInvoiceAsync(id, dto, userId, GetUserName());
                await _systemLogService.LogSuccessAsync($"Supplier invoice updated: {invoice.InvoiceNumber}", "Purchases", "update", userId, GetUserName(), "SupplierInvoice", id.ToString());
                return Ok(new { success = true, data = invoice });
            }
            catch (KeyNotFoundException) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Invoice not found" } }); }
            // The service prefixes its message with a bracketed code. Genuine
            // status-machine violations are 409 INVALID_TRANSITION; plain input
            // validation (negative AmountPaid, overpayment, …) is 400 so the FE
            // does not mis-branch on a transition error that never happened.
            catch (InvalidOperationException ex)
            {
                if (ex.Message.StartsWith("[INVALID_TRANSITION]", StringComparison.Ordinal))
                    return Conflict(new { success = false, error = new { code = "INVALID_TRANSITION", message = ex.Message } });
                if (ex.Message.StartsWith("[OVERPAYMENT]", StringComparison.Ordinal))
                    return Conflict(new { success = false, error = new { code = "OVERPAYMENT", message = ex.Message } });
                return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating supplier invoice {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "delete")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteInvoice(int id)
        {
            try
            {
                var userId = GetUserId();
                if (!await _service.DeleteInvoiceAsync(id, userId, GetUserName()))
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Invoice not found" } });
                await _systemLogService.LogSuccessAsync($"Supplier invoice deleted: {id}", "Purchases", "delete", userId, GetUserName(), "SupplierInvoice", id.ToString());
                return Ok(new { success = true, message = "Deleted successfully" });
            }
            // Business-rule refusals (recorded payments, already declared to the DGI) are
            // NOT server faults: returning 500 with a generic French message made the UI
            // silently restore the row with no explanation. Surface them as 409 + reason.
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Supplier invoice {Id} delete refused: {Message}", id, ex.Message);
                return Conflict(new { success = false, error = new { code = "DELETE_NOT_ALLOWED", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting supplier invoice {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }

        }

        /// <summary>
        /// Records a Facture en Ligne (TTN) submission for this invoice.
        ///
        /// There is NO automated TTN transmission in this system: the previous version of
        /// this endpoint flipped the status to "sent" without transmitting anything, which
        /// made the UI claim a filing that never happened. It now only records a
        /// submission the user performed on the TTN portal, and requires the reference
        /// returned by that portal as proof.
        /// </summary>
        [RequirePermission("purchases", "update")]
        [HttpPost("{id:int}/facture-en-ligne")]
        public async Task<IActionResult> RecordFactureEnLigneSubmission(int id, [FromBody] RecordFactureEnLigneDto? body)
        {
            try
            {
                if (body == null || string.IsNullOrWhiteSpace(body.FactureEnLigneId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = new
                        {
                            code = "FEL_REFERENCE_REQUIRED",
                            message = "No automated Facture en Ligne transmission is configured. Submit the invoice on the TTN portal and record the reference it returned."
                        }
                    });
                }

                var userId = GetUserId();
                var status = string.IsNullOrWhiteSpace(body.Status) ? "sent" : body.Status!.Trim();
                var allowed = new[] { "pending", "sent", "validated", "rejected" };
                if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
                    return BadRequest(new { success = false, error = new { code = "BAD_REQUEST", message = $"Status must be one of: {string.Join(", ", allowed)}" } });

                var dto = new UpdateSupplierInvoiceDto
                {
                    FactureEnLigneId = body.FactureEnLigneId!.Trim(),
                    FactureEnLigneStatus = status.ToLowerInvariant(),
                    FactureEnLigneSentAt = body.SentAt ?? DateTime.UtcNow,
                };
                var invoice = await _service.UpdateInvoiceAsync(id, dto, userId, GetUserName());
                await _systemLogService.LogSuccessAsync($"Facture en ligne submission recorded ({dto.FactureEnLigneId}) for {invoice.InvoiceNumber}", "Purchases", "update", userId, GetUserName(), "SupplierInvoice", id.ToString());
                return Ok(new { success = true, data = invoice });
            }
            catch (KeyNotFoundException) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Invoice not found" } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending facture en ligne for invoice {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // ── Activity timeline ──
        [RequirePermission("purchases", "read")]
        [HttpGet("{id:int}/activities")]
        public async Task<IActionResult> GetActivities(int id, [FromQuery] int page = 1, [FromQuery] int limit = 50)
        {
            try
            {
                var activities = await _service.GetActivitiesAsync(id, page, limit);
                return Ok(new { success = true, data = activities });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching activities for invoice {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // ── Items (only allowed when invoice.status == 'draft') ──
        [RequirePermission("purchases", "create")]
        [HttpPost("{id:int}/items")]
        public async Task<IActionResult> AddItem(int id, [FromBody] CreateSupplierInvoiceItemDto dto)
        {
            try
            {
                var item = await _service.AddItemAsync(id, dto, GetUserId(), GetUserName());
                return Ok(new { success = true, data = item });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = new { code = "BAD_REQUEST", message = ex.Message } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to invoice {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "update")]
        [HttpPatch("{id:int}/items/{itemId:int}")]
        public async Task<IActionResult> UpdateItem(int id, int itemId, [FromBody] CreateSupplierInvoiceItemDto dto)
        {
            try
            {
                var item = await _service.UpdateItemAsync(id, itemId, dto, GetUserId(), GetUserName());
                return Ok(new { success = true, data = item });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = new { code = "BAD_REQUEST", message = ex.Message } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item {ItemId} on invoice {Id}", itemId, id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "delete")]
        [HttpDelete("{id:int}/items/{itemId:int}")]
        public async Task<IActionResult> DeleteItem(int id, int itemId)
        {
            try
            {
                if (!await _service.DeleteItemAsync(id, itemId, GetUserId(), GetUserName()))
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Item not found" } });
                return Ok(new { success = true, message = "Deleted" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = new { code = "BAD_REQUEST", message = ex.Message } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item {ItemId} on invoice {Id}", itemId, id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }
    }
}
