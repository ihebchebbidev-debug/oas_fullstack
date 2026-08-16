using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Deals.DTOs;
using MyApi.Modules.Deals.Services;
using System.Security.Claims;

namespace MyApi.Modules.Deals.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/deals")]
    public class DealsController : ControllerBase
    {
        private readonly IDealService _dealService;
        private readonly ILogger<DealsController> _logger;

        public DealsController(IDealService dealService, ILogger<DealsController> logger)
        {
            _dealService = dealService;
            _logger = logger;
        }

        private string GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        private string GetCurrentUserName() =>
            User.FindFirst(ClaimTypes.Name)?.Value ??
            ((User.FindFirst("FirstName")?.Value + " " + User.FindFirst("LastName")?.Value)?.Trim()) ??
            User.FindFirst(ClaimTypes.Email)?.Value ?? "anonymous";

        private IActionResult Error(string code = "INTERNAL_ERROR", string message = "An error occurred", int status = 500)
            => StatusCode(status, new { success = false, error = new { code, message } });

        [HttpGet]
        public async Task<IActionResult> GetDeals(
            [FromQuery] string? stage = null,
            [FromQuery] string? category = null,
            [FromQuery] string? source = null,
            [FromQuery] string? contact_id = null,
            [FromQuery] string? project_id = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20,
            [FromQuery] string sort_by = "updated_at",
            [FromQuery] string sort_order = "desc")
        {
            try
            {
                var result = await _dealService.GetDealsAsync(stage, category, source, contact_id, project_id, search, page, limit, sort_by, sort_order);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching deals");
                return Error(message: "An error occurred while fetching deals");
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats([FromQuery] DateTime? date_from = null, [FromQuery] DateTime? date_to = null)
        {
            try
            {
                var stats = await _dealService.GetDealStatsAsync(date_from, date_to);
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching deal stats");
                return Error(message: "An error occurred while fetching statistics");
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetDealById(int id)
        {
            try
            {
                var deal = await _dealService.GetDealByIdAsync(id);
                if (deal == null) return NotFound(new { success = false, error = new { code = "DEAL_NOT_FOUND", message = "Deal not found" } });
                return Ok(new { success = true, data = deal });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching deal {DealId}", id);
                return Error(message: "An error occurred while fetching the deal");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateDeal([FromBody] CreateDealDto createDto)
        {
            try
            {
                var deal = await _dealService.CreateDealAsync(createDto, GetCurrentUserId(), GetCurrentUserName());
                return CreatedAtAction(nameof(GetDealById), new { id = deal.Id }, new { success = true, data = deal });
            }
            catch (InvalidOperationException ex)
            {
                // e.g. INVALID_STAGE — a client mistake, not a server fault.
                return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating deal");
                return Error(message: "An error occurred while creating the deal");
            }
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> UpdateDeal(int id, [FromBody] UpdateDealDto updateDto)
        {
            try
            {
                var deal = await _dealService.UpdateDealAsync(id, updateDto, GetCurrentUserId(), GetCurrentUserName());
                if (deal == null) return NotFound(new { success = false, error = new { code = "DEAL_NOT_FOUND", message = "Deal not found" } });
                return Ok(new { success = true, data = deal });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating deal {DealId}", id);
                return Error(message: "An error occurred while updating the deal");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteDeal(int id)
        {
            try
            {
                var ok = await _dealService.DeleteDealAsync(id, GetCurrentUserId());
                if (!ok) return NotFound(new { success = false, error = new { code = "DEAL_NOT_FOUND", message = "Deal not found" } });
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting deal {DealId}", id);
                return Error(message: "An error occurred while deleting the deal");
            }
        }

        [HttpPost("{id:int}/convert")]
        public async Task<IActionResult> ConvertDeal(int id, [FromBody] ConvertDealDto convertDto)
        {
            try
            {
                var result = await _dealService.ConvertDealAsync(id, convertDto, GetCurrentUserId(), GetCurrentUserName());
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { code = "DEAL_NOT_FOUND", message = ex.Message } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "CONVERSION_ERROR", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting deal {DealId}", id);
                return Error(message: "An error occurred while converting the deal");
            }
        }

        // ── Items ──

        [HttpPost("{id:int}/items")]
        public async Task<IActionResult> AddItem(int id, [FromBody] CreateDealItemDto itemDto)
        {
            try
            {
                var item = await _dealService.AddDealItemAsync(id, itemDto, GetCurrentUserId(), GetCurrentUserName());
                if (item == null) return NotFound(new { success = false, error = new { code = "DEAL_NOT_FOUND", message = "Deal not found" } });
                return Ok(new { success = true, data = item });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to deal {DealId}", id);
                return Error(message: "An error occurred while adding the item");
            }
        }

        [HttpPatch("{id:int}/items/{itemId:int}")]
        public async Task<IActionResult> UpdateItem(int id, int itemId, [FromBody] CreateDealItemDto itemDto)
        {
            try
            {
                var item = await _dealService.UpdateDealItemAsync(id, itemId, itemDto, GetCurrentUserId(), GetCurrentUserName());
                if (item == null) return NotFound(new { success = false, error = new { code = "ITEM_NOT_FOUND", message = "Item not found" } });
                return Ok(new { success = true, data = item });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item {ItemId}", itemId);
                return Error(message: "An error occurred while updating the item");
            }
        }

        [HttpDelete("{id:int}/items/{itemId:int}")]
        public async Task<IActionResult> DeleteItem(int id, int itemId)
        {
            try
            {
                var ok = await _dealService.DeleteDealItemAsync(id, itemId, GetCurrentUserId(), GetCurrentUserName());
                if (!ok) return NotFound(new { success = false, error = new { code = "ITEM_NOT_FOUND", message = "Item not found" } });
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item {ItemId}", itemId);
                return Error(message: "An error occurred while deleting the item");
            }
        }

        // ── Activities ──

        [HttpGet("{id:int}/activities")]
        public async Task<IActionResult> GetActivities(int id, [FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            try
            {
                var (activities, total) = await _dealService.GetDealActivitiesAsync(id, type, page, limit);
                return Ok(new { success = true, data = new { activities, pagination = new { page, limit, total } } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching activities for deal {DealId}", id);
                return Error(message: "An error occurred while fetching activities");
            }
        }

        [HttpPost("{id:int}/activities")]
        public async Task<IActionResult> AddActivity(int id, [FromBody] CreateDealActivityDto activityDto)
        {
            try
            {
                var activity = await _dealService.AddDealActivityAsync(id, activityDto, GetCurrentUserId(), GetCurrentUserName());
                if (activity == null) return NotFound(new { success = false, error = new { code = "DEAL_NOT_FOUND", message = "Deal not found" } });
                return Ok(new { success = true, data = activity });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding activity to deal {DealId}", id);
                return Error(message: "An error occurred while adding the activity");
            }
        }

        [HttpDelete("{id:int}/activities/{activityId:int}")]
        public async Task<IActionResult> DeleteActivity(int id, int activityId)
        {
            try
            {
                var ok = await _dealService.DeleteDealActivityAsync(id, activityId, GetCurrentUserId(), GetCurrentUserName());
                if (!ok) return NotFound(new { success = false, error = new { code = "ACTIVITY_NOT_FOUND", message = "Activity not found" } });
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting activity {ActivityId}", activityId);
                return Error(message: "An error occurred while deleting the activity");
            }
        }
    }
}
