using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Shared.Services;

namespace MyApi.Modules.Shared.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogsController : ControllerBase
    {
        private readonly IInMemoryLogStore _logStore;
        private readonly ILogger<LogsController> _logger;

        public LogsController(IInMemoryLogStore logStore, ILogger<LogsController> logger)
        {
            _logStore = logStore;
            _logger = logger;
        }

        /// <summary>
        /// Get recent backend logs (max 500)
        /// </summary>
        /// <param name="count">Number of logs to return (default: 100, max: 500)</param>
        /// <param name="level">Filter by log level (Information, Warning, Error)</param>
        [HttpGet]
        public ActionResult<object> GetLogs([FromQuery] int count = 100, [FromQuery] string? level = null)
        {
            _logger.LogInformation("Fetching {Count} logs with level filter: {Level}", count, level ?? "all");
            
            var logs = _logStore.GetLogs(Math.Min(count, 500), level);
            
            return Ok(new
            {
                count = logs.Count(),
                totalInStore = _logStore.Count,
                logs = logs
            });
        }

        /// <summary>
        /// Clear all stored logs
        /// </summary>
        [HttpDelete]
        public ActionResult ClearLogs()
        {
            _logger.LogInformation("Clearing all in-memory logs");
            _logStore.Clear();
            return Ok(new { message = "Logs cleared", count = 0 });
        }

        /// <summary>
        /// Add a test log entry (for debugging)
        /// </summary>
        [HttpPost("test")]
        public ActionResult TestLog([FromQuery] string message = "Test log message", [FromQuery] string level = "Information")
        {
            switch (level.ToLower())
            {
                case "error":
                    _logger.LogError(message);
                    break;
                case "warning":
                    _logger.LogWarning(message);
                    break;
                default:
                    _logger.LogInformation(message);
                    break;
            }
            
            return Ok(new { message = $"Logged: {message}", level });
        }
    }
}
