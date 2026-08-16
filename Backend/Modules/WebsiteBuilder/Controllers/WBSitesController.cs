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
    public class WBSitesController : ControllerBase
    {
        private readonly IWBSiteService _siteService;
        private readonly ILogger<WBSitesController> _logger;

        public WBSitesController(IWBSiteService siteService, ILogger<WBSitesController> logger)
        {
            _siteService = siteService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<WBSiteListResponseDto>> GetAllSites([FromQuery] WBSiteSearchRequestDto? searchRequest = null)
        {
            try
            {
                var result = await _siteService.GetAllSitesAsync(searchRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting WB sites");
                return StatusCode(500, "An error occurred while retrieving sites");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WBSiteResponseDto>> GetSite(int id)
        {
            try
            {
                var site = await _siteService.GetSiteByIdAsync(id);
                if (site == null) return NotFound($"Site with ID {id} not found");
                return Ok(site);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting WB site {SiteId}", id);
                return StatusCode(500, "An error occurred while retrieving the site");
            }
        }

        [HttpGet("slug/{slug}")]
        public async Task<ActionResult<WBSiteResponseDto>> GetSiteBySlug(string slug)
        {
            try
            {
                var site = await _siteService.GetSiteBySlugAsync(slug);
                if (site == null) return NotFound($"Site with slug '{slug}' not found");
                return Ok(site);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting WB site by slug {Slug}", slug);
                return StatusCode(500, "An error occurred while retrieving the site");
            }
        }

        [HttpPost]
        public async Task<ActionResult<WBSiteResponseDto>> CreateSite([FromBody] CreateWBSiteRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var site = await _siteService.CreateSiteAsync(createDto, GetCurrentUser());
                return CreatedAtAction(nameof(GetSite), new { id = site.Id }, site);
            }
            catch (WBSlugConflictException ex) { return Conflict(new { error = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating WB site");
                return StatusCode(500, "An error occurred while creating the site");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<WBSiteResponseDto>> UpdateSite(int id, [FromBody] UpdateWBSiteRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var site = await _siteService.UpdateSiteAsync(id, updateDto, GetCurrentUser());
                if (site == null) return NotFound($"Site with ID {id} not found");
                return Ok(site);
            }
            catch (WBSlugConflictException ex) { return Conflict(new { error = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating WB site {SiteId}", id);
                return StatusCode(500, "An error occurred while updating the site");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteSite(int id)
        {
            try
            {
                var success = await _siteService.DeleteSiteAsync(id, GetCurrentUser());
                if (!success) return NotFound($"Site with ID {id} not found");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting WB site {SiteId}", id);
                return StatusCode(500, "An error occurred while deleting the site");
            }
        }

        [HttpPost("{id}/duplicate")]
        public async Task<ActionResult<WBSiteResponseDto>> DuplicateSite(int id)
        {
            try
            {
                var site = await _siteService.DuplicateSiteAsync(id, GetCurrentUser());
                if (site == null) return NotFound($"Site with ID {id} not found");
                return CreatedAtAction(nameof(GetSite), new { id = site.Id }, site);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error duplicating WB site {SiteId}", id);
                return StatusCode(500, "An error occurred while duplicating the site");
            }
        }

        [HttpPost("{id}/publish")]
        public async Task<ActionResult<WBSiteResponseDto>> PublishSite(int id)
        {
            try
            {
                var site = await _siteService.PublishSiteAsync(id, GetCurrentUser());
                if (site == null) return NotFound($"Site with ID {id} not found");
                return Ok(site);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing WB site {SiteId}", id);
                return StatusCode(500, "An error occurred while publishing the site");
            }
        }

        [HttpPost("{id}/unpublish")]
        public async Task<ActionResult<WBSiteResponseDto>> UnpublishSite(int id)
        {
            try
            {
                var site = await _siteService.UnpublishSiteAsync(id, GetCurrentUser());
                if (site == null) return NotFound($"Site with ID {id} not found");
                return Ok(site);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpublishing WB site {SiteId}", id);
                return StatusCode(500, "An error occurred while unpublishing the site");
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
