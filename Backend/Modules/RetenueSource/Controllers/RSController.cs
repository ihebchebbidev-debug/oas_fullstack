using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Infrastructure;
using MyApi.Modules.RetenueSource.DTOs;
using MyApi.Modules.RetenueSource.Services;
using System.Security.Claims;

namespace MyApi.Modules.RetenueSource.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/retenue-source")]
    public class RSController : ControllerBase
    {
        private readonly IRSService _rsService;
        private readonly ILogger<RSController> _logger;

        public RSController(IRSService rsService, ILogger<RSController> logger)
        {
            _rsService = rsService;
            _logger = logger;
        }

        private string GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

        // ─── CRUD ───

        /// <summary>
        /// GET /api/retenue-source — List RS records with filters
        /// </summary>
        [RequirePermission("purchases", "read")]
        [HttpGet]
        public async Task<IActionResult> GetRSRecords(
            [FromQuery] string? entity_type = null,
            [FromQuery] int? entity_id = null,
            [FromQuery] int? month = null,
            [FromQuery] int? year = null,
            [FromQuery] string? status = null,
            [FromQuery] string? supplier_tax_id = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20)
        {
            try
            {
                var result = await _rsService.GetRSRecordsAsync(
                    entity_type, entity_id, month, year,
                    status, supplier_tax_id, search, page, limit);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching RS records");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// GET /api/retenue-source/{id}
        /// </summary>
        [RequirePermission("purchases", "read")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetRSRecordById(int id)
        {
            try
            {
                var record = await _rsService.GetRSRecordByIdAsync(id);
                if (record == null)
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "RS record not found" } });
                return Ok(new { success = true, data = record });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching RS record {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// POST /api/retenue-source — Create RS record
        /// </summary>
        [RequirePermission("purchases", "create")]
        [HttpPost]
        public async Task<IActionResult> CreateRSRecord([FromBody] CreateRSRecordDto dto)
        {
            try
            {
                var record = await _rsService.CreateRSRecordAsync(dto, GetCurrentUserId());
                return CreatedAtAction(nameof(GetRSRecordById), new { id = record.Id },
                    new { success = true, data = record });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { success = false, error = new { code = "DUPLICATE", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating RS record");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// PATCH /api/retenue-source/{id} — Update RS record
        /// </summary>
        [RequirePermission("purchases", "update")]
        [HttpPatch("{id:int}")]
        public async Task<IActionResult> UpdateRSRecord(int id, [FromBody] UpdateRSRecordDto dto)
        {
            try
            {
                var record = await _rsService.UpdateRSRecordAsync(id, dto, GetCurrentUserId());
                return Ok(new { success = true, data = record });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "RS record not found" } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "INVALID_OPERATION", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating RS record {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// DELETE /api/retenue-source/{id}
        /// </summary>
        [RequirePermission("purchases", "delete")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteRSRecord(int id)
        {
            try
            {
                var result = await _rsService.DeleteRSRecordAsync(id);
                if (!result)
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "RS record not found" } });
                return Ok(new { success = true, message = "RS record deleted successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "INVALID_OPERATION", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting RS record {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // ─── Calculation ───

        /// <summary>
        /// GET /api/retenue-source/calculate?amount_paid=1000&amp;rs_type_code=10
        /// </summary>
        [RequirePermission("purchases", "read")]
        [HttpGet("calculate")]
        public IActionResult CalculateRS([FromQuery] decimal amount_paid, [FromQuery] string rs_type_code = "10")
        {
            try
            {
                var result = _rsService.CalculateRS(amount_paid, rs_type_code);
                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
            }
        }

        // ─── TEJ Export ───

        /// <summary>
        /// POST /api/retenue-source/tej-export — Generate TEJ XML and save as document
        /// </summary>
        [RequirePermission("purchases", "update")]
        [HttpPost("tej-export")]
        public async Task<IActionResult> ExportTEJ([FromBody] TEJExportRequestDto request)
        {
            try
            {
                var result = await _rsService.ExportTEJAsync(request, GetCurrentUserId());
                return Ok(new { success = true, data = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "NO_RECORDS", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting TEJ");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        /// <summary>
        /// GET /api/retenue-source/tej-logs — TEJ export history
        /// </summary>
        [RequirePermission("purchases", "read")]
        [HttpGet("tej-logs")]
        public async Task<IActionResult> GetTEJExportLogs([FromQuery] int? year = null)
        {
            try
            {
                var logs = await _rsService.GetTEJExportLogsAsync(year);
                return Ok(new { success = true, data = logs });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching TEJ logs");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // ─── Stats ───

        /// <summary>
        /// GET /api/retenue-source/stats
        /// </summary>
        [RequirePermission("purchases", "read")]
        [HttpGet("stats")]
        public async Task<IActionResult> GetRSStats(
            [FromQuery] string? entity_type = null,
            [FromQuery] int? entity_id = null,
            [FromQuery] int? month = null,
            [FromQuery] int? year = null)
        {
            try
            {
                var stats = await _rsService.GetRSStatsAsync(entity_type, entity_id, month, year);
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching RS stats");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }
    }
}
