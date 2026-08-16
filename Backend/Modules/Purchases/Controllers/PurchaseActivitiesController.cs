using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Infrastructure;
using MyApi.Modules.Purchases.Services;

namespace MyApi.Modules.Purchases.Controllers
{
    /// <summary>
    /// Cross-entity purchase audit log. Replaces the previous client-side fan-out
    /// (fetch N orders → N per-order activity calls → in-memory sort), which only
    /// ever surfaced a slice of the history. Filtering, sorting and paging are
    /// done in SQL here.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/purchase-activities")]
    public class PurchaseActivitiesController : ControllerBase
    {
        private readonly IPurchaseOrderService _service;
        private readonly ILogger<PurchaseActivitiesController> _logger;

        public PurchaseActivitiesController(IPurchaseOrderService service, ILogger<PurchaseActivitiesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [RequirePermission("audit_logs", "read")]
        [HttpGet]
        public async Task<IActionResult> GetActivities(
            [FromQuery] string? entityType = null,
            [FromQuery] int? entityId = null,
            [FromQuery] string? activityType = null,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            try
            {
                var result = await _service.GetAllActivitiesAsync(
                    entityType, entityId, activityType, search, dateFrom, dateTo, page, limit);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching purchase activities");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }
    }
}
