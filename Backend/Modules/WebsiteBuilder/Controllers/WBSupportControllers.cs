using MyApi.Modules.WebsiteBuilder.DTOs;
using MyApi.Modules.WebsiteBuilder.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MyApi.Modules.WebsiteBuilder.Controllers
{
    // ══════════════════════════════════════════════════════════════
    // Global Blocks Controller
    // ══════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WBGlobalBlocksController : ControllerBase
    {
        private readonly IWBGlobalBlockService _service;
        private readonly ILogger<WBGlobalBlocksController> _logger;

        public WBGlobalBlocksController(IWBGlobalBlockService service, ILogger<WBGlobalBlocksController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<WBGlobalBlockResponseDto>>> GetAll()
        {
            try { return Ok(await _service.GetAllGlobalBlocksAsync()); }
            catch (Exception ex) { _logger.LogError(ex, "Error getting global blocks"); return StatusCode(500, "Error retrieving global blocks"); }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WBGlobalBlockResponseDto>> GetById(int id)
        {
            try
            {
                var block = await _service.GetGlobalBlockByIdAsync(id);
                return block != null ? Ok(block) : NotFound();
            }
            catch (Exception ex) { _logger.LogError(ex, "Error getting global block {Id}", id); return StatusCode(500, "Error retrieving global block"); }
        }

        [HttpPost]
        public async Task<ActionResult<WBGlobalBlockResponseDto>> Create([FromBody] CreateWBGlobalBlockRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var block = await _service.CreateGlobalBlockAsync(dto, GetCurrentUser());
                return CreatedAtAction(nameof(GetById), new { id = block.Id }, block);
            }
            catch (Exception ex) { _logger.LogError(ex, "Error creating global block"); return StatusCode(500, "Error creating global block"); }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<WBGlobalBlockResponseDto>> Update(int id, [FromBody] UpdateWBGlobalBlockRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var block = await _service.UpdateGlobalBlockAsync(id, dto, GetCurrentUser());
                return block != null ? Ok(block) : NotFound();
            }
            catch (Exception ex) { _logger.LogError(ex, "Error updating global block {Id}", id); return StatusCode(500, "Error updating global block"); }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try { return await _service.DeleteGlobalBlockAsync(id, GetCurrentUser()) ? NoContent() : NotFound(); }
            catch (Exception ex) { _logger.LogError(ex, "Error deleting global block {Id}", id); return StatusCode(500, "Error deleting global block"); }
        }

        [HttpPost("{id}/usage")]
        public async Task<ActionResult> TrackUsage(int id, [FromQuery] int siteId, [FromQuery] int? pageId)
        {
            try { await _service.TrackUsageAsync(id, siteId, pageId); return NoContent(); }
            catch (Exception ex) { _logger.LogError(ex, "Error tracking global block usage"); return StatusCode(500, "Error tracking usage"); }
        }

        private string GetCurrentUser() => User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "system";
    }

    // ══════════════════════════════════════════════════════════════
    // Brand Profiles Controller
    // ══════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WBBrandProfilesController : ControllerBase
    {
        private readonly IWBBrandProfileService _service;
        private readonly ILogger<WBBrandProfilesController> _logger;

        public WBBrandProfilesController(IWBBrandProfileService service, ILogger<WBBrandProfilesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<WBBrandProfileResponseDto>>> GetAll()
        {
            try { return Ok(await _service.GetAllBrandProfilesAsync()); }
            catch (Exception ex) { _logger.LogError(ex, "Error getting brand profiles"); return StatusCode(500, "Error retrieving brand profiles"); }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WBBrandProfileResponseDto>> GetById(int id)
        {
            try
            {
                var profile = await _service.GetBrandProfileByIdAsync(id);
                return profile != null ? Ok(profile) : NotFound();
            }
            catch (Exception ex) { _logger.LogError(ex, "Error getting brand profile {Id}", id); return StatusCode(500, "Error retrieving brand profile"); }
        }

        [HttpPost]
        public async Task<ActionResult<WBBrandProfileResponseDto>> Create([FromBody] CreateWBBrandProfileRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var profile = await _service.CreateBrandProfileAsync(dto, GetCurrentUser());
                return CreatedAtAction(nameof(GetById), new { id = profile.Id }, profile);
            }
            catch (Exception ex) { _logger.LogError(ex, "Error creating brand profile"); return StatusCode(500, "Error creating brand profile"); }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<WBBrandProfileResponseDto>> Update(int id, [FromBody] UpdateWBBrandProfileRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var profile = await _service.UpdateBrandProfileAsync(id, dto, GetCurrentUser());
                return profile != null ? Ok(profile) : NotFound();
            }
            catch (Exception ex) { _logger.LogError(ex, "Error updating brand profile {Id}", id); return StatusCode(500, "Error updating brand profile"); }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try { return await _service.DeleteBrandProfileAsync(id, GetCurrentUser()) ? NoContent() : NotFound(); }
            catch (Exception ex) { _logger.LogError(ex, "Error deleting brand profile {Id}", id); return StatusCode(500, "Error deleting brand profile"); }
        }

        private string GetCurrentUser() => User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "system";
    }

    // ══════════════════════════════════════════════════════════════
    // Form Submissions Controller
    // ══════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WBFormSubmissionsController : ControllerBase
    {
        private readonly IWBFormSubmissionService _service;
        private readonly ILogger<WBFormSubmissionsController> _logger;

        public WBFormSubmissionsController(IWBFormSubmissionService service, ILogger<WBFormSubmissionsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("site/{siteId}")]
        public async Task<ActionResult<WBFormSubmissionListResponseDto>> GetBySiteId(int siteId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            try { return Ok(await _service.GetSubmissionsBySiteIdAsync(siteId, pageNumber, pageSize)); }
            catch (Exception ex) { _logger.LogError(ex, "Error getting form submissions for site {SiteId}", siteId); return StatusCode(500, "Error retrieving submissions"); }
        }

        /// <summary>
        /// Authenticated endpoint for admin-created submissions (e.g. testing, manual entry).
        /// Public visitors should use POST /api/public/sites/{slug}/forms instead.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<WBFormSubmissionResponseDto>> Create([FromBody] CreateWBFormSubmissionRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var submission = await _service.CreateSubmissionAsync(dto, ipAddress);
                return Ok(submission);
            }
            catch (Exception ex) { _logger.LogError(ex, "Error creating form submission"); return StatusCode(500, "Error submitting form"); }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try { return await _service.DeleteSubmissionAsync(id) ? NoContent() : NotFound(); }
            catch (Exception ex) { _logger.LogError(ex, "Error deleting form submission {Id}", id); return StatusCode(500, "Error deleting submission"); }
        }

        [HttpDelete("site/{siteId}/clear")]
        public async Task<ActionResult> Clear(int siteId, [FromQuery] string? formComponentId = null)
        {
            try { await _service.ClearSubmissionsAsync(siteId, formComponentId); return NoContent(); }
            catch (Exception ex) { _logger.LogError(ex, "Error clearing submissions for site {SiteId}", siteId); return StatusCode(500, "Error clearing submissions"); }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Media Controller
    // ══════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WBMediaController : ControllerBase
    {
        private readonly IWBMediaService _service;
        private readonly ILogger<WBMediaController> _logger;

        public WBMediaController(IWBMediaService service, ILogger<WBMediaController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<WBMediaResponseDto>>> GetAll([FromQuery] int? siteId, [FromQuery] string? folder)
        {
            try { return Ok(await _service.GetMediaAsync(siteId, folder)); }
            catch (Exception ex) { _logger.LogError(ex, "Error getting media"); return StatusCode(500, "Error retrieving media"); }
        }

        [HttpPost]
        public async Task<ActionResult<WBMediaResponseDto>> Create([FromBody] CreateWBMediaRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var currentUser = User.FindFirst(ClaimTypes.Email)?.Value ?? "system";
                var media = await _service.CreateMediaAsync(dto, currentUser);
                return Ok(media);
            }
            catch (Exception ex) { _logger.LogError(ex, "Error creating media record"); return StatusCode(500, "Error creating media"); }
        }

        // [HttpDelete("{id}")]  — REMOVED: use DELETE /api/WBUpload/{mediaId} to also clean up disk files
        // Soft-delete media metadata only. For full cleanup (disk + DB),
        // use DELETE /api/WBUpload/{mediaId} instead.
        // This endpoint is intentionally removed to prevent orphaned files.
        // All media deletion should go through WBUploadController.DeleteMedia.
    }

    // ══════════════════════════════════════════════════════════════
    // Templates Controller
    // ══════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WBTemplatesController : ControllerBase
    {
        private readonly IWBTemplateService _service;
        private readonly ILogger<WBTemplatesController> _logger;

        public WBTemplatesController(IWBTemplateService service, ILogger<WBTemplatesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<WBTemplateResponseDto>>> GetAll()
        {
            try { return Ok(await _service.GetAllTemplatesAsync()); }
            catch (Exception ex) { _logger.LogError(ex, "Error getting templates"); return StatusCode(500, "Error retrieving templates"); }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WBTemplateResponseDto>> GetById(int id)
        {
            try
            {
                var template = await _service.GetTemplateByIdAsync(id);
                return template != null ? Ok(template) : NotFound();
            }
            catch (Exception ex) { _logger.LogError(ex, "Error getting template {Id}", id); return StatusCode(500, "Error retrieving template"); }
        }

        [HttpGet("categories")]
        public async Task<ActionResult<List<string>>> GetCategories()
        {
            try { return Ok(await _service.GetTemplateCategoriesAsync()); }
            catch (Exception ex) { _logger.LogError(ex, "Error getting template categories"); return StatusCode(500, "Error retrieving categories"); }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Activity Log Controller
    // ══════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WBActivityLogController : ControllerBase
    {
        private readonly IWBActivityLogService _service;
        private readonly ILogger<WBActivityLogController> _logger;

        public WBActivityLogController(IWBActivityLogService service, ILogger<WBActivityLogController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("site/{siteId}")]
        public async Task<ActionResult<List<WBActivityLogResponseDto>>> GetBySiteId(int siteId, [FromQuery] int count = 50)
        {
            try { return Ok(await _service.GetActivityLogAsync(siteId, count)); }
            catch (Exception ex) { _logger.LogError(ex, "Error getting activity log for site {SiteId}", siteId); return StatusCode(500, "Error retrieving activity log"); }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Public Site Renderer — serves published sites to visitors (no auth)
    // ══════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/public/sites")]
    public class WBPublicSiteController : ControllerBase
    {
        private readonly IWBSiteService _siteService;
        private readonly IWBFormSubmissionService _formService;
        private readonly ILogger<WBPublicSiteController> _logger;

        // ── Anti-abuse: in-memory sliding-window throttle for the public form
        // submission endpoint. Keyed by client IP. Replace with a distributed
        // store (Redis) once horizontal scaling is enabled.
        private const int FormSubmissionsPerMinutePerIp = 5;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentQueue<DateTime>> _formHits = new();

        public WBPublicSiteController(
            IWBSiteService siteService,
            IWBFormSubmissionService formService,
            ILogger<WBPublicSiteController> logger)
        {
            _siteService = siteService;
            _formService = formService;
            _logger = logger;
        }

        /// <summary>
        /// Get a published site by slug — public, no auth required.
        /// Uses the published-only lookup which bypasses the global tenant
        /// filter safely (anonymous requests have no tenant header) while
        /// guaranteeing only opted-in sites are exposed.
        /// </summary>
        [HttpGet("{slug}")]
        public async Task<ActionResult<WBSiteResponseDto>> GetPublishedSite(string slug)
        {
            try
            {
                var site = await _siteService.GetPublishedSiteBySlugAsync(slug);
                if (site == null) return NotFound(new { error = $"Site '{slug}' not found" });
                return Ok(site);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving public site {Slug}", slug);
                return StatusCode(500, new { error = "Error loading site" });
            }
        }

        /// <summary>
        /// Submit a form on a published site — public, no auth required.
        /// Protected by:
        ///   1. Honeypot: if the request body contains a non-empty "_hp" field
        ///      (a hidden input that humans never fill in) we silently 200.
        ///   2. Per-IP sliding-window throttle (5 submissions/min/IP).
        /// </summary>
        [HttpPost("{slug}/forms")]
        public async Task<ActionResult<WBFormSubmissionResponseDto>> SubmitForm(
            string slug,
            [FromBody] CreateWBFormSubmissionRequestDto dto)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                // ── Rate limit ──
                var queue = _formHits.GetOrAdd(ipAddress, _ => new System.Collections.Concurrent.ConcurrentQueue<DateTime>());
                var now = DateTime.UtcNow;
                var cutoff = now.AddMinutes(-1);
                while (queue.TryPeek(out var oldest) && oldest < cutoff) queue.TryDequeue(out _);
                if (queue.Count >= FormSubmissionsPerMinutePerIp)
                {
                    _logger.LogWarning("Public form rate-limit hit for IP {Ip} on site {Slug}", ipAddress, slug);
                    return StatusCode(429, new { error = "Too many submissions. Please wait a minute and try again." });
                }

                // ── Verify site is published (bypass tenant filter safely) ──
                var site = await _siteService.GetPublishedSiteBySlugAsync(slug);
                if (site == null)
                    return NotFound(new { error = "Site not found or not published" });

                // ── Honeypot check: if "_hp" key exists with any value, drop. ──
                // We accept silently (200) so bots can't distinguish success from rejection.
                if (!string.IsNullOrEmpty(dto.DataJson) && LooksLikeHoneypotHit(dto.DataJson))
                {
                    _logger.LogInformation("Honeypot triggered for IP {Ip} on site {Slug}", ipAddress, slug);
                    return Ok(new WBFormSubmissionResponseDto { Id = 0, SiteId = site.Id, SubmittedAt = now });
                }

                // ── Validate DataJson is well-formed JSON before persisting ──
                try { _ = System.Text.Json.JsonDocument.Parse(dto.DataJson ?? "{}"); }
                catch { return BadRequest(new { error = "Invalid form payload" }); }

                // Override SiteId to match the slug (prevent spoofing)
                dto.SiteId = site.Id;

                queue.Enqueue(now);
                var submission = await _formService.CreateSubmissionAsync(dto, ipAddress);
                return Ok(submission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting form on public site {Slug}", slug);
                return StatusCode(500, new { error = "Error submitting form" });
            }
        }

        private static bool LooksLikeHoneypotHit(string dataJson)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(dataJson);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return false;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.NameEquals("_hp") || prop.NameEquals("website_url") || prop.NameEquals("hp_field"))
                    {
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(prop.Value.GetString()))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch { return false; }
        }
    }
}
