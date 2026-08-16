using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Shared.DTOs;
using MyApi.Modules.Shared.Services;
using System.Text;

namespace MyApi.Modules.Shared.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemLogsController : ControllerBase
    {
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<SystemLogsController> _logger;

        public SystemLogsController(ISystemLogService systemLogService, ILogger<SystemLogsController> logger)
        {
            _systemLogService = systemLogService;
            _logger = logger;
        }

        /// <summary>
        /// Get all system logs with optional filtering and pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<SystemLogListResponseDto>> GetLogs(
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? level = null,
            [FromQuery] string? module = null,
            [FromQuery] string? action = null,
            [FromQuery] string? userId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var searchRequest = new SystemLogSearchRequestDto
                {
                    SearchTerm = searchTerm,
                    Level = level,
                    Module = module,
                    Action = action,
                    UserId = userId,
                    StartDate = startDate,
                    EndDate = endDate,
                    PageNumber = pageNumber,
                    PageSize = Math.Min(pageSize, 100) // Max 100 per page
                };

                var result = await _systemLogService.GetLogsAsync(searchRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching system logs");
                return StatusCode(500, new { message = "An error occurred while fetching logs" });
            }
        }

        /// <summary>
        /// Get a specific log entry by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<SystemLogDto>> GetLogById(int id)
        {
            try
            {
                var log = await _systemLogService.GetLogByIdAsync(id);
                if (log == null)
                {
                    return NotFound(new { message = $"Log with ID {id} not found" });
                }
                return Ok(log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching log {LogId}", id);
                return StatusCode(500, new { message = "An error occurred while fetching the log" });
            }
        }

        /// <summary>
        /// Create a new log entry
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SystemLogDto>> CreateLog([FromBody] CreateSystemLogRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Get client IP address
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                
                var log = await _systemLogService.CreateLogAsync(createDto, ipAddress);
                return CreatedAtAction(nameof(GetLogById), new { id = log.Id }, log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating system log");
                return StatusCode(500, new { message = "An error occurred while creating the log" });
            }
        }

        /// <summary>
        /// Get log statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<SystemLogStatisticsDto>> GetStatistics()
        {
            try
            {
                var stats = await _systemLogService.GetStatisticsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching log statistics");
                return StatusCode(500, new { message = "An error occurred while fetching statistics" });
            }
        }

        /// <summary>
        /// Get list of available modules for filtering
        /// </summary>
        [HttpGet("modules")]
        public async Task<ActionResult<List<string>>> GetModules()
        {
            try
            {
                var modules = await _systemLogService.GetModulesAsync();
                return Ok(modules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching modules");
                return StatusCode(500, new { message = "An error occurred while fetching modules" });
            }
        }

        /// <summary>
        /// Delete logs older than specified days (default 7 days). Use daysOld=0 to delete ALL logs.
        /// </summary>
        [HttpDelete("cleanup")]
        public async Task<ActionResult<CleanupResultDto>> CleanupOldLogs([FromQuery] int daysOld = 7)
        {
            try
            {
                if (daysOld < 0)
                {
                    return BadRequest(new { message = "daysOld must be 0 or greater" });
                }

                var result = await _systemLogService.CleanupOldLogsAsync(daysOld);
                
                // Log the cleanup action itself
                var logMessage = daysOld == 0 
                    ? $"System logs cleanup: Deleted all {result.DeletedCount} logs"
                    : $"System logs cleanup: Deleted {result.DeletedCount} logs older than {daysOld} days";
                    
                await _systemLogService.LogInfoAsync(logMessage, "SystemLogs", "delete");
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during log cleanup");
                return StatusCode(500, new { message = "An error occurred during cleanup" });
            }
        }

        /// <summary>
        /// Export logs to CSV
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportLogs(
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? level = null,
            [FromQuery] string? module = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var searchRequest = new SystemLogSearchRequestDto
                {
                    SearchTerm = searchTerm,
                    Level = level,
                    Module = module,
                    StartDate = startDate,
                    EndDate = endDate,
                    PageNumber = 1,
                    PageSize = 10000 // Export up to 10k logs
                };

                var result = await _systemLogService.GetLogsAsync(searchRequest);

                // Build CSV
                var csv = new StringBuilder();
                csv.AppendLine("Timestamp,Level,Module,Action,Message,User,Entity Type,Entity ID,Details,IP Address");

                foreach (var log in result.Logs)
                {
                    csv.AppendLine($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{log.Level}\",\"{EscapeCsv(log.Module)}\",\"{log.Action}\",\"{EscapeCsv(log.Message)}\",\"{EscapeCsv(log.UserName ?? log.UserId ?? "")}\",\"{EscapeCsv(log.EntityType ?? "")}\",\"{EscapeCsv(log.EntityId ?? "")}\",\"{EscapeCsv(log.Details ?? "")}\",\"{log.IpAddress ?? ""}\"");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                var fileName = $"system-logs-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.csv";

                // Log the export action
                await _systemLogService.LogInfoAsync(
                    $"Exported {result.Logs.Count} system logs to CSV",
                    "SystemLogs",
                    "export"
                );

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting logs");
                return StatusCode(500, new { message = "An error occurred while exporting logs" });
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        }
    }
}
