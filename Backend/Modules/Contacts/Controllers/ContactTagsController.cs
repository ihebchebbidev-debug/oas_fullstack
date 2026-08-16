using MyApi.Modules.Contacts.DTOs;
using MyApi.Modules.Contacts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyApi.Modules.Contacts.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ContactTagsController : ControllerBase
    {
        private readonly IContactTagService _contactTagService;
        private readonly ILogger<ContactTagsController> _logger;

        public ContactTagsController(IContactTagService contactTagService, ILogger<ContactTagsController> logger)
        {
            _contactTagService = contactTagService;
            _logger = logger;
        }

        /// <summary>
        /// Get all contact tags
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ContactTagListResponseDto>> GetAllTags()
        {
            try
            {
                var result = await _contactTagService.GetAllTagsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all contact tags");
                return StatusCode(500, "An error occurred while retrieving tags");
            }
        }

        /// <summary>
        /// Get tag by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ContactTagDto>> GetTag(int id)
        {
            try
            {
                var tag = await _contactTagService.GetTagByIdAsync(id);
                
                if (tag == null)
                {
                    return NotFound($"Tag with ID {id} not found");
                }

                return Ok(tag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tag with ID {TagId}", id);
                return StatusCode(500, "An error occurred while retrieving the tag");
            }
        }

        /// <summary>
        /// Create a new tag
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ContactTagDto>> CreateTag([FromBody] CreateContactTagRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var tag = await _contactTagService.CreateTagAsync(createDto);

                return CreatedAtAction(nameof(GetTag), new { id = tag.Id }, tag);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while creating tag");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tag");
                return StatusCode(500, "An error occurred while creating the tag");
            }
        }

        /// <summary>
        /// Update an existing tag
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ContactTagDto>> UpdateTag(int id, [FromBody] UpdateContactTagRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var tag = await _contactTagService.UpdateTagAsync(id, updateDto);

                if (tag == null)
                {
                    return NotFound($"Tag with ID {id} not found");
                }

                return Ok(tag);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while updating tag with ID {TagId}", id);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tag with ID {TagId}", id);
                return StatusCode(500, "An error occurred while updating the tag");
            }
        }

        /// <summary>
        /// Delete a tag (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTag(int id)
        {
            try
            {
                var success = await _contactTagService.DeleteTagAsync(id);

                if (!success)
                {
                    return NotFound($"Tag with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tag with ID {TagId}", id);
                return StatusCode(500, "An error occurred while deleting the tag");
            }
        }

        /// <summary>
        /// Check if tag exists by name
        /// </summary>
        [HttpGet("exists/{name}")]
        public async Task<ActionResult<bool>> TagExists(string name)
        {
            try
            {
                var exists = await _contactTagService.TagExistsAsync(name);
                return Ok(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if tag exists with name {TagName}", name);
                return StatusCode(500, "An error occurred while checking tag existence");
            }
        }
    }
}
