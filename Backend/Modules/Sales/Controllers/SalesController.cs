using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Data;
using MyApi.Modules.Sales.DTOs;
using MyApi.Modules.Sales.Models;
using MyApi.Modules.Sales.Services;
using MyApi.Modules.Shared.Services;
using System.Security.Claims;

namespace MyApi.Modules.Sales.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/sales")]
    public class SalesController : ControllerBase
    {
        private readonly ISaleService _saleService;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<SalesController> _logger;
        private readonly ApplicationDbContext _context;

        public SalesController(
            ISaleService saleService, 
            ISystemLogService systemLogService,
            ILogger<SalesController> logger,
            ApplicationDbContext context)
        {
            _saleService = saleService;
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

        [HttpGet]
        public async Task<IActionResult> GetSales(
            [FromQuery] string? status = null,
            [FromQuery] string? stage = null,
            [FromQuery] string? priority = null,
            [FromQuery] string? contact_id = null,
            [FromQuery] DateTime? date_from = null,
            [FromQuery] DateTime? date_to = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20,
            [FromQuery] string sort_by = "updated_at",
            [FromQuery] string sort_order = "desc"
        )
        {
            try
            {
                var result = await _saleService.GetSalesAsync(
                    status, stage, priority, contact_id,
                    date_from, date_to, search,
                    page, limit, sort_by, sort_order
                );
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sales");
                await _systemLogService.LogErrorAsync("Failed to retrieve sales", "Sales", "read", GetCurrentUserId(), GetCurrentUserName(), details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(
            [FromQuery] DateTime? date_from = null,
            [FromQuery] DateTime? date_to = null
        )
        {
            try
            {
                var stats = await _saleService.GetSaleStatsAsync(date_from, date_to);
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sale statistics");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetSaleById(int id)
        {
            try
            {
                var sale = await _saleService.GetSaleByIdAsync(id);
                if (sale == null)
                {
                    return NotFound(new { success = false, error = new { code = "SALE_NOT_FOUND", message = "Sale not found" } });
                }
                return Ok(new { success = true, data = sale });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sale {SaleId}", id);
                await _systemLogService.LogErrorAsync($"Failed to retrieve sale {id}", "Sales", "read", GetCurrentUserId(), GetCurrentUserName(), "Sale", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateSale([FromBody] CreateSaleDto createDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var sale = await _saleService.CreateSaleAsync(createDto, userId);

                await _systemLogService.LogSuccessAsync($"Sale created: {sale.SaleNumber}", "Sales", "create", userId, GetCurrentUserName(), "Sale", sale.Id.ToString());

                return CreatedAtAction(nameof(GetSaleById), new { id = sale.Id }, new { success = true, data = sale });
            }
            catch (KeyNotFoundException ex)
            {
                await _systemLogService.LogWarningAsync($"Failed to create sale: {ex.Message}", "Sales", "create", GetCurrentUserId(), GetCurrentUserName(), "Sale", details: ex.Message);
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sale");
                await _systemLogService.LogErrorAsync("Failed to create sale", "Sales", "create", GetCurrentUserId(), GetCurrentUserName(), "Sale", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("from-offer/{offerId:int}")]
        public async Task<IActionResult> CreateSaleFromOffer(int offerId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var sale = await _saleService.CreateSaleFromOfferAsync(offerId, userId);

                await _systemLogService.LogSuccessAsync($"Sale created from offer {offerId}: {sale.SaleNumber}", "Sales", "create", userId, GetCurrentUserName(), "Sale", sale.Id.ToString());

                return CreatedAtAction(nameof(GetSaleById), new { id = sale.Id }, new { success = true, data = sale });
            }
            catch (KeyNotFoundException ex)
            {
                await _systemLogService.LogWarningAsync($"Failed to create sale from offer {offerId}: {ex.Message}", "Sales", "create", GetCurrentUserId(), GetCurrentUserName(), "Sale", details: ex.Message);
                return NotFound(new { success = false, error = new { code = "OFFER_NOT_FOUND", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sale from offer");
                await _systemLogService.LogErrorAsync($"Failed to create sale from offer {offerId}", "Sales", "create", GetCurrentUserId(), GetCurrentUserName(), "Sale", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> UpdateSale(int id, [FromBody] UpdateSaleDto updateDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var sale = await _saleService.UpdateSaleAsync(id, updateDto, userId);

                await _systemLogService.LogSuccessAsync($"Sale updated: {sale.SaleNumber}", "Sales", "update", userId, GetCurrentUserName(), "Sale", id.ToString());

                return Ok(new { success = true, data = sale });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "SALE_NOT_FOUND", message = "Sale not found" } });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { success = false, error = new { code = "SALE_LOCKED", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sale {SaleId}", id);
                await _systemLogService.LogErrorAsync($"Failed to update sale {id}", "Sales", "update", GetCurrentUserId(), GetCurrentUserName(), "Sale", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteSale(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _saleService.DeleteSaleAsync(id, userId);
                if (!result)
                {
                    return NotFound(new { success = false, error = new { code = "SALE_NOT_FOUND", message = "Sale not found" } });
                }

                await _systemLogService.LogSuccessAsync($"Sale deleted: ID {id}", "Sales", "delete", userId, GetCurrentUserName(), "Sale", id.ToString());

                return Ok(new { success = true, message = "Sale deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sale {SaleId}", id);
                await _systemLogService.LogErrorAsync($"Failed to delete sale {id}", "Sales", "delete", GetCurrentUserId(), GetCurrentUserName(), "Sale", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("{id:int}/activities")]
        public async Task<IActionResult> GetSaleActivities(int id, [FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            try
            {
                var activities = await _saleService.GetSaleActivitiesAsync(id, type, page, limit);
                return Ok(new { success = true, data = new { activities, pagination = new { page, limit, total = activities.Count } } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching activities for sale {SaleId}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/activities")]
        public async Task<IActionResult> AddSaleActivity(int id, [FromBody] CreateSaleActivityDto activityDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userName = GetCurrentUserName();
                
                // Verify sale exists
                var sale = await _saleService.GetSaleByIdAsync(id);
                if (sale == null)
                    return NotFound(new { success = false, error = new { code = "SALE_NOT_FOUND", message = "Sale not found" } });

                var activity = new SaleActivity
                {
                    SaleId = id,
                    Type = activityDto.Type,
                    Description = activityDto.Description,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByName = userName
                };

                // Update sale's LastActivity and persist activity in a single save
                var saleEntity = await _context.Sales.FindAsync(id);
                if (saleEntity != null) saleEntity.LastActivity = DateTime.UtcNow;
                _context.SaleActivities.Add(activity);
                await _context.SaveChangesAsync();

                await _systemLogService.LogSuccessAsync($"Activity added to sale {id}: {activityDto.Type}", "Sales", "create", userId, userName, "SaleActivity", activity.Id.ToString());

                return Created($"/api/sales/{id}/activities/{activity.Id}", new { 
                    success = true, 
                    data = new SaleActivityDto
                    {
                        Id = activity.Id,
                        SaleId = activity.SaleId,
                        Type = activity.Type,
                        Description = activity.Description ?? "",
                        CreatedAt = activity.CreatedAt,
                        CreatedByName = activity.CreatedByName
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding activity to sale {SaleId}", id);
                await _systemLogService.LogErrorAsync($"Failed to add activity to sale {id}", "Sales", "create", GetCurrentUserId(), GetCurrentUserName(), "SaleActivity", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/items")]
        public async Task<IActionResult> AddSaleItem(int id, [FromBody] CreateSaleItemDto itemDto)
        {
            try
            {
                var item = await _saleService.AddSaleItemAsync(id, itemDto);

                await _systemLogService.LogSuccessAsync($"Item added to sale {id}", "Sales", "create", GetCurrentUserId(), GetCurrentUserName(), "SaleItem", item.Id.ToString());

                return CreatedAtAction(nameof(GetSaleById), new { id }, new { success = true, data = item });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "SALE_NOT_FOUND", message = "Sale not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to sale {SaleId}", id);
                await _systemLogService.LogErrorAsync($"Failed to add item to sale {id}", "Sales", "create", GetCurrentUserId(), GetCurrentUserName(), "SaleItem", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPatch("{id:int}/items/{itemId:int}")]
        public async Task<IActionResult> UpdateSaleItem(int id, int itemId, [FromBody] CreateSaleItemDto itemDto)
        {
            try
            {
                var item = await _saleService.UpdateSaleItemAsync(id, itemId, itemDto);

                await _systemLogService.LogSuccessAsync($"Item {itemId} updated in sale {id}", "Sales", "update", GetCurrentUserId(), GetCurrentUserName(), "SaleItem", itemId.ToString());

                return Ok(new { success = true, data = item });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { code = "ITEM_NOT_FOUND", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item {ItemId} in sale {SaleId}", itemId, id);
                await _systemLogService.LogErrorAsync($"Failed to update item {itemId} in sale {id}", "Sales", "update", GetCurrentUserId(), GetCurrentUserName(), "SaleItem", itemId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpDelete("{id:int}/items/{itemId:int}")]
        public async Task<IActionResult> DeleteSaleItem(int id, int itemId)
        {
            try
            {
                var result = await _saleService.DeleteSaleItemAsync(id, itemId);
                if (!result)
                {
                    return NotFound(new { success = false, error = new { code = "ITEM_NOT_FOUND", message = "Item not found" } });
                }

                await _systemLogService.LogSuccessAsync($"Item {itemId} deleted from sale {id}", "Sales", "delete", GetCurrentUserId(), GetCurrentUserName(), "SaleItem", itemId.ToString());

                return Ok(new { success = true, message = "Item deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item {ItemId} from sale {SaleId}", itemId, id);
                await _systemLogService.LogErrorAsync($"Failed to delete item {itemId} from sale {id}", "Sales", "delete", GetCurrentUserId(), GetCurrentUserName(), "SaleItem", itemId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }
    }
}
