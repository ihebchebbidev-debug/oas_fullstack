using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Offers.DTOs;
using MyApi.Modules.Offers.Services;
using MyApi.Modules.Shared.Services;
using System.Security.Claims;

namespace MyApi.Modules.Offers.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/offers")]
    public class OffersController : ControllerBase
    {
        private readonly IOfferService _offerService;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<OffersController> _logger;

        public OffersController(
            IOfferService offerService, 
            ISystemLogService systemLogService,
            ILogger<OffersController> logger)
        {
            _offerService = offerService;
            _systemLogService = systemLogService;
            _logger = logger;
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
        public async Task<IActionResult> GetOffers(
            [FromQuery] string? status = null,
            [FromQuery] string? category = null,
            [FromQuery] string? source = null,
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
                var result = await _offerService.GetOffersAsync(
                    status, category, source, contact_id,
                    date_from, date_to, search,
                    page, limit, sort_by, sort_order
                );

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offers");
                await _systemLogService.LogErrorAsync("Failed to retrieve offers", "Offers", "read", GetCurrentUserId(), GetCurrentUserName(), details: ex.Message);
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
                var stats = await _offerService.GetOfferStatsAsync(date_from, date_to);
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offer statistics");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOfferById(int id)
        {
            try
            {
                var offer = await _offerService.GetOfferByIdAsync(id);
                if (offer == null)
                {
                    return NotFound(new { success = false, error = new { code = "OFFER_NOT_FOUND", message = "Offer not found" } });
                }
                return Ok(new { success = true, data = offer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offer {OfferId}", id);
                await _systemLogService.LogErrorAsync($"Failed to retrieve offer {id}", "Offers", "read", GetCurrentUserId(), GetCurrentUserName(), "Offer", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateOffer([FromBody] CreateOfferDto createDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var offer = await _offerService.CreateOfferAsync(createDto, userId);

                await _systemLogService.LogSuccessAsync($"Offer created: {offer.OfferNumber}", "Offers", "create", userId, GetCurrentUserName(), "Offer", offer.Id.ToString());

                return CreatedAtAction(nameof(GetOfferById), new { id = offer.Id }, new { success = true, data = offer });
            }
            catch (KeyNotFoundException ex)
            {
                await _systemLogService.LogWarningAsync($"Failed to create offer: {ex.Message}", "Offers", "create", GetCurrentUserId(), GetCurrentUserName(), "Offer", details: ex.Message);
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating offer");
                await _systemLogService.LogErrorAsync("Failed to create offer", "Offers", "create", GetCurrentUserId(), GetCurrentUserName(), "Offer", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> UpdateOffer(int id, [FromBody] UpdateOfferDto updateDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var offer = await _offerService.UpdateOfferAsync(id, updateDto, userId);

                await _systemLogService.LogSuccessAsync($"Offer updated: {offer.OfferNumber}", "Offers", "update", userId, GetCurrentUserName(), "Offer", id.ToString());

                return Ok(new { success = true, data = offer });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "OFFER_NOT_FOUND", message = "Offer not found" } });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { success = false, error = new { code = "OFFER_LOCKED", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating offer {OfferId}", id);
                await _systemLogService.LogErrorAsync($"Failed to update offer {id}", "Offers", "update", GetCurrentUserId(), GetCurrentUserName(), "Offer", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteOffer(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _offerService.DeleteOfferAsync(id, userId);
                if (!result)
                {
                    return NotFound(new { success = false, error = new { code = "OFFER_NOT_FOUND", message = "Offer not found" } });
                }

                await _systemLogService.LogSuccessAsync($"Offer deleted: ID {id}", "Offers", "delete", GetCurrentUserId(), GetCurrentUserName(), "Offer", id.ToString());

                return Ok(new { success = true, message = "Offer deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting offer {OfferId}", id);
                await _systemLogService.LogErrorAsync($"Failed to delete offer {id}", "Offers", "delete", GetCurrentUserId(), GetCurrentUserName(), "Offer", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/renew")]
        public async Task<IActionResult> RenewOffer(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var renewedOffer = await _offerService.RenewOfferAsync(id, userId);

                await _systemLogService.LogSuccessAsync($"Offer renewed: {renewedOffer.OfferNumber} (from ID {id})", "Offers", "create", userId, GetCurrentUserName(), "Offer", renewedOffer.Id.ToString());

                return CreatedAtAction(nameof(GetOfferById), new { id = renewedOffer.Id }, new { success = true, data = renewedOffer });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "OFFER_NOT_FOUND", message = "Offer not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renewing offer {OfferId}", id);
                await _systemLogService.LogErrorAsync($"Failed to renew offer {id}", "Offers", "create", GetCurrentUserId(), GetCurrentUserName(), "Offer", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/send")]
        public async Task<IActionResult> MarkOfferAsSent(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var sentOffer = await _offerService.MarkOfferAsSentAsync(id, userId);

                await _systemLogService.LogSuccessAsync($"Offer marked as sent: {sentOffer.OfferNumber}", "Offers", "update", userId, GetCurrentUserName(), "Offer", id.ToString());

                return Ok(new { success = true, data = sentOffer });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "OFFER_NOT_FOUND", message = "Offer not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking offer {OfferId} as sent", id);
                await _systemLogService.LogErrorAsync($"Failed to mark offer {id} as sent", "Offers", "update", GetCurrentUserId(), GetCurrentUserName(), "Offer", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/convert")]
        public async Task<IActionResult> ConvertOffer(int id, [FromBody] ConvertOfferDto convertDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _offerService.ConvertOfferAsync(id, convertDto, userId);

                var convertType = convertDto.ConvertToSale ? "Sale" : (convertDto.ConvertToServiceOrder ? "ServiceOrder" : "Unknown");
                await _systemLogService.LogSuccessAsync($"Offer {id} converted to {convertType}", "Offers", "update", userId, GetCurrentUserName(), "Offer", id.ToString());

                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "OFFER_NOT_FOUND", message = "Offer not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting offer {OfferId}", id);
                await _systemLogService.LogErrorAsync($"Failed to convert offer {id}", "Offers", "update", GetCurrentUserId(), GetCurrentUserName(), "Offer", id.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpGet("{id:int}/activities")]
        public async Task<IActionResult> GetOfferActivities(int id, [FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            try
            {
                var activities = await _offerService.GetOfferActivitiesAsync(id, type, page, limit);
                return Ok(new { success = true, data = new { activities, pagination = new { page, limit, total = activities.Count } } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching activities for offer {OfferId}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/activities")]
        public async Task<IActionResult> AddOfferActivity(int id, [FromBody] CreateOfferActivityDto activityDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var activity = await _offerService.AddOfferActivityAsync(id, activityDto, userId);

                await _systemLogService.LogSuccessAsync($"Activity added to offer {id}: {activityDto.Type}", "Offers", "create", GetCurrentUserId(), GetCurrentUserName(), "OfferActivity", activity.Id.ToString());

                return CreatedAtAction(nameof(GetOfferActivities), new { id }, new { success = true, data = activity });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "OFFER_NOT_FOUND", message = "Offer not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding activity to offer {OfferId}", id);
                await _systemLogService.LogErrorAsync($"Failed to add activity to offer {id}", "Offers", "create", GetCurrentUserId(), GetCurrentUserName(), "OfferActivity", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpDelete("{id:int}/activities/{activityId:int}")]
        public async Task<IActionResult> DeleteOfferActivity(int id, int activityId)
        {
            try
            {
                var result = await _offerService.DeleteOfferActivityAsync(id, activityId);
                if (!result)
                {
                    return NotFound(new { success = false, error = new { code = "ACTIVITY_NOT_FOUND", message = "Activity not found" } });
                }

                await _systemLogService.LogSuccessAsync($"Activity {activityId} deleted from offer {id}", "Offers", "delete", GetCurrentUserId(), GetCurrentUserName(), "OfferActivity", activityId.ToString());

                return Ok(new { success = true, message = "Activity deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting activity {ActivityId} from offer {OfferId}", activityId, id);
                await _systemLogService.LogErrorAsync($"Failed to delete activity {activityId} from offer {id}", "Offers", "delete", GetCurrentUserId(), GetCurrentUserName(), "OfferActivity", activityId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPost("{id:int}/items")]
        public async Task<IActionResult> AddOfferItem(int id, [FromBody] CreateOfferItemDto itemDto)
        {
            try
            {
                var item = await _offerService.AddOfferItemAsync(id, itemDto);

                await _systemLogService.LogSuccessAsync($"Item added to offer {id}", "Offers", "create", GetCurrentUserId(), GetCurrentUserName(), "OfferItem", item.Id.ToString());

                return CreatedAtAction(nameof(GetOfferById), new { id }, new { success = true, data = item });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, error = new { code = "OFFER_NOT_FOUND", message = "Offer not found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to offer {OfferId}", id);
                await _systemLogService.LogErrorAsync($"Failed to add item to offer {id}", "Offers", "create", GetCurrentUserId(), GetCurrentUserName(), "OfferItem", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpPatch("{id:int}/items/{itemId:int}")]
        public async Task<IActionResult> UpdateOfferItem(int id, int itemId, [FromBody] CreateOfferItemDto itemDto)
        {
            try
            {
                var item = await _offerService.UpdateOfferItemAsync(id, itemId, itemDto);

                await _systemLogService.LogSuccessAsync($"Item {itemId} updated in offer {id}", "Offers", "update", GetCurrentUserId(), GetCurrentUserName(), "OfferItem", itemId.ToString());

                return Ok(new { success = true, data = item });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { code = "ITEM_NOT_FOUND", message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item {ItemId} in offer {OfferId}", itemId, id);
                await _systemLogService.LogErrorAsync($"Failed to update item {itemId} in offer {id}", "Offers", "update", GetCurrentUserId(), GetCurrentUserName(), "OfferItem", itemId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [HttpDelete("{id:int}/items/{itemId:int}")]
        public async Task<IActionResult> DeleteOfferItem(int id, int itemId)
        {
            try
            {
                var result = await _offerService.DeleteOfferItemAsync(id, itemId);
                if (!result)
                {
                    return NotFound(new { success = false, error = new { code = "ITEM_NOT_FOUND", message = "Item not found" } });
                }

                await _systemLogService.LogSuccessAsync($"Item {itemId} deleted from offer {id}", "Offers", "delete", GetCurrentUserId(), GetCurrentUserName(), "OfferItem", itemId.ToString());

                return Ok(new { success = true, message = "Item deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item {ItemId} from offer {OfferId}", itemId, id);
                await _systemLogService.LogErrorAsync($"Failed to delete item {itemId} from offer {id}", "Offers", "delete", GetCurrentUserId(), GetCurrentUserName(), "OfferItem", itemId.ToString(), ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        // =====================================================
        // Bulk Import Endpoint - Supports up to 10,000+ records
        // =====================================================

        /// <summary>
        /// Bulk import offers with batch processing for high performance.
        /// Supports up to 10,000+ records with automatic batching.
        /// </summary>
        [HttpPost("import")]
        public async Task<IActionResult> BulkImportOffers([FromBody] BulkImportOfferRequestDto importRequest)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userName = GetCurrentUserName();

                _logger.LogInformation("Starting bulk import of {Count} offers by user {UserId}", 
                    importRequest.Offers.Count, userId);

                var result = await _offerService.BulkImportOffersAsync(importRequest, userId);

                await _systemLogService.LogSuccessAsync(
                    $"Bulk imported {result.SuccessCount} offers ({result.FailedCount} failures, {result.SkippedCount} skipped)", 
                    "Offers", "import", userId, userName, "Offer");

                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk import of offers");
                await _systemLogService.LogErrorAsync("Bulk import failed", "Offers", "import", GetCurrentUserId(), GetCurrentUserName(), "Offer", details: ex.Message);
                return StatusCode(500, new { success = false, error = new { code = "BULK_IMPORT_FAILED", message = "An error occurred during bulk import", details = ex.Message } });
            }
        }
    }
}
