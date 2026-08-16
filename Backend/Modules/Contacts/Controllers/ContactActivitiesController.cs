using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Contacts.DTOs;
using MyApi.Modules.Contacts.Services;

namespace MyApi.Modules.Contacts.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ContactActivitiesController : ControllerBase
    {
        private readonly IContactActivityService _service;
        private readonly ILogger<ContactActivitiesController> _logger;

        public ContactActivitiesController(IContactActivityService service, ILogger<ContactActivitiesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Get the unified activity feed for a contact (offers, sales, service orders,
        /// dispatches, installations, notes). Sorted newest first.
        /// </summary>
        [HttpGet("contact/{contactId}")]
        public async Task<ActionResult<ContactActivityListResponseDto>> GetByContactId(
            int contactId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            try
            {
                var result = await _service.GetByContactIdAsync(contactId, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting activities for contact {ContactId}", contactId);
                return StatusCode(500, "An error occurred while retrieving contact activities");
            }
        }
    }
}
