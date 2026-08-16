using MyApi.Modules.WebsiteBuilder.DTOs;
using MyApi.Modules.WebsiteBuilder.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MyApi.Modules.WebsiteBuilder.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WBPagesController : ControllerBase
    {
        private readonly IWBPageService _pageService;
        private readonly ILogger<WBPagesController> _logger;

        public WBPagesController(IWBPageService pageService, ILogger<WBPagesController> logger)
        {
            _pageService = pageService;
            _logger = logger;
        }

        [HttpGet("site/{siteId}")]
        public async Task<ActionResult<List<WBPageResponseDto>>> GetPagesBySiteId(int siteId)
        {
            try
            {
                var pages = await _pageService.GetPagesBySiteIdAsync(siteId);
                return Ok(pages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pages for site {SiteId}", siteId);
                return StatusCode(500, "An error occurred while retrieving pages");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WBPageResponseDto>> GetPage(int id)
        {
            try
            {
                var page = await _pageService.GetPageByIdAsync(id);
                if (page == null) return NotFound($"Page with ID {id} not found");
                return Ok(page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting page {PageId}", id);
                return StatusCode(500, "An error occurred while retrieving the page");
            }
        }

        [HttpPost]
        public async Task<ActionResult<WBPageResponseDto>> CreatePage([FromBody] CreateWBPageRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var page = await _pageService.CreatePageAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetPage), new { id = page.Id }, page);
            }
            catch (WBSlugConflictException ex) { return Conflict(new { error = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating page for site {SiteId}", createDto.SiteId);
                return StatusCode(500, "An error occurred while creating the page");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<WBPageResponseDto>> UpdatePage(int id, [FromBody] UpdateWBPageRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var page = await _pageService.UpdatePageAsync(id, updateDto, GetCurrentUser());
                if (page == null) return NotFound($"Page with ID {id} not found");
                return Ok(page);
            }
            catch (WBConcurrencyException ex)
            {
                return Conflict(new { error = ex.Message, currentUpdatedAt = ex.CurrentUpdatedAt });
            }
            catch (WBSlugConflictException ex) { return Conflict(new { error = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating page {PageId}", id);
                return StatusCode(500, "An error occurred while updating the page");
            }
        }

        [HttpPut("{id}/components")]
        public async Task<ActionResult<WBPageSaveResultDto>> UpdatePageComponents(int id, [FromBody] UpdateWBPageComponentsRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var result = await _pageService.UpdatePageComponentsAsync(id, updateDto, GetCurrentUser());
                if (result == null) return NotFound($"Page with ID {id} not found");
                return Ok(result);
            }
            catch (WBConcurrencyException ex)
            {
                // 409 Conflict — surface current server timestamp so the client
                // can refetch the latest page state and retry from there.
                return Conflict(new { error = ex.Message, currentUpdatedAt = ex.CurrentUpdatedAt });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating page components {PageId}", id);
                return StatusCode(500, "An error occurred while updating page components");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeletePage(int id)
        {
            try
            {
                var success = await _pageService.DeletePageAsync(id, GetCurrentUser());
                if (!success) return NotFound($"Page with ID {id} not found");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting page {PageId}", id);
                return StatusCode(500, "An error occurred while deleting the page");
            }
        }

        [HttpPut("reorder")]
        public async Task<ActionResult> ReorderPages([FromBody] ReorderWBPagesRequestDto reorderDto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var success = await _pageService.ReorderPagesAsync(reorderDto, GetCurrentUser());
                if (!success) return BadRequest("Failed to reorder pages");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering pages for site {SiteId}", reorderDto.SiteId);
                return StatusCode(500, "An error occurred while reordering pages");
            }
        }

        // ── Versioning ──

        [HttpGet("{id}/versions")]
        public async Task<ActionResult<List<WBPageVersionResponseDto>>> GetPageVersions(int id)
        {
            try
            {
                var versions = await _pageService.GetPageVersionsAsync(id);
                return Ok(versions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting versions for page {PageId}", id);
                return StatusCode(500, "An error occurred while retrieving page versions");
            }
        }

        [HttpPost("{id}/versions")]
        public async Task<ActionResult<WBPageVersionResponseDto>> SavePageVersion(int id, [FromBody] CreateWBPageVersionRequestDto createDto)
        {
            try
            {
                var version = await _pageService.SavePageVersionAsync(id, createDto, GetCurrentUser());
                return Ok(version);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving version for page {PageId}", id);
                return StatusCode(500, "An error occurred while saving the page version");
            }
        }

        [HttpPost("{id}/versions/{versionId}/restore")]
        public async Task<ActionResult> RestorePageVersion(int id, int versionId)
        {
            try
            {
                var success = await _pageService.RestorePageVersionAsync(id, versionId, GetCurrentUser());
                if (!success) return NotFound("Version or page not found");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring version {VersionId} for page {PageId}", versionId, id);
                return StatusCode(500, "An error occurred while restoring the page version");
            }
        }

        private string GetCurrentUser()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ??
                   User.FindFirst(ClaimTypes.Name)?.Value ??
                   User.FindFirst("email")?.Value ??
                   "system";
        }
    }
}
