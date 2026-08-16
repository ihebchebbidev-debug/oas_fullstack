using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Lookups.DTOs;
using MyApi.Modules.Lookups.Services;
using MyApi.Modules.Shared.Services;

namespace MyApi.Modules.Articles.Controllers
{
    /// <summary>
    /// Article Groups Controller - Manages article groups using the generic Lookups system
    /// 
    /// NOTE: ArticleGroups now use the generic LookupItems table with LookupType='article-groups'
    /// This controller is a convenience wrapper around the ILookupService for article-groups lookups.
    /// The separate ArticleGroups table has been dropped in favor of the Lookups system.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/articles/groups")]
    public class ArticleGroupsController : ControllerBase
    {
        private readonly ILookupService _lookupService;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<ArticleGroupsController> _logger;

        public ArticleGroupsController(
            ILookupService lookupService,
            ISystemLogService systemLogService,
            ILogger<ArticleGroupsController> logger)
        {
            _lookupService = lookupService;
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

        /// <summary>
        /// Get all article groups
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<LookupListResponseDto>> GetArticleGroups()
        {
            try
            {
                _logger.LogInformation("Fetching article groups");
                var groups = await _lookupService.GetArticleGroupsAsync();
                return Ok(groups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching article groups");
                return StatusCode(500, new { message = "Error fetching article groups", error = ex.Message });
            }
        }

        /// <summary>
        /// Get article group by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<LookupItemDto>> GetArticleGroupById(int id)
        {
            try
            {
                _logger.LogInformation("Fetching article group with ID: {id}", id);
                var group = await _lookupService.GetArticleGroupByIdAsync(id);
                if (group == null)
                    return NotFound(new { message = "Article group not found" });
                return Ok(group);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching article group {id}", id);
                return StatusCode(500, new { message = "Error fetching article group", error = ex.Message });
            }
        }

        /// <summary>
        /// Create a new article group
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<LookupItemDto>> CreateArticleGroup([FromBody] CreateLookupItemRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                _logger.LogInformation("Creating new article group: {name}", createDto.Name);
                var userId = GetCurrentUserId();
                var group = await _lookupService.CreateArticleGroupAsync(createDto, userId);

                await _systemLogService.LogSuccessAsync(
                    $"Created article group: {createDto.Name}",
                    "Articles",
                    "CREATE",
                    userId,
                    GetCurrentUserName(),
                    "ArticleGroup",
                    group.Id.ToString());

                return CreatedAtAction(nameof(GetArticleGroupById), new { id = group.Id }, group);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating article group");
                return StatusCode(500, new { message = "Error creating article group", error = ex.Message });
            }
        }

        /// <summary>
        /// Update an article group
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<LookupItemDto>> UpdateArticleGroup(int id, [FromBody] UpdateLookupItemRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                _logger.LogInformation("Updating article group with ID: {id}", id);
                var userId = GetCurrentUserId();
                var group = await _lookupService.UpdateArticleGroupAsync(id, updateDto, userId);

                if (group == null)
                    return NotFound(new { message = "Article group not found" });

                await _systemLogService.LogSuccessAsync(
                    $"Updated article group: {updateDto.Name}",
                    "Articles",
                    "UPDATE",
                    userId,
                    GetCurrentUserName(),
                    "ArticleGroup",
                    id.ToString());

                return Ok(group);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating article group {id}", id);
                return StatusCode(500, new { message = "Error updating article group", error = ex.Message });
            }
        }

        /// <summary>
        /// Delete an article group
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteArticleGroup(int id)
        {
            try
            {
                _logger.LogInformation("Deleting article group with ID: {id}", id);
                var userId = GetCurrentUserId();
                var result = await _lookupService.DeleteArticleGroupAsync(id, userId);

                if (!result)
                    return NotFound(new { message = "Article group not found" });

                await _systemLogService.LogSuccessAsync(
                    "Deleted article group",
                    "Articles",
                    "DELETE",
                    userId,
                    GetCurrentUserName(),
                    "ArticleGroup",
                    id.ToString());

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting article group {id}", id);
                return StatusCode(500, new { message = "Error deleting article group", error = ex.Message });
            }
        }
    }
}
