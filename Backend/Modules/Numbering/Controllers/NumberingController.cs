using MyApi.Modules.Numbering.DTOs;
using MyApi.Modules.Numbering.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyApi.Modules.Numbering.Controllers
{
    [ApiController]
    [Route("api/settings/numbering")]
    [Authorize]
    public class NumberingController : ControllerBase
    {
        private readonly INumberingService _numberingService;
        private readonly ILogger<NumberingController> _logger;

        public NumberingController(INumberingService numberingService, ILogger<NumberingController> logger)
        {
            _numberingService = numberingService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/settings/numbering — return all entity numbering settings with preview
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllSettings()
        {
            try
            {
                var settings = await _numberingService.GetAllSettingsAsync();
                return Ok(new NumberingListResponse
                {
                    Success = true,
                    Message = "Numbering settings retrieved",
                    Data = settings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching numbering settings");
                return StatusCode(500, new NumberingResponse { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// GET /api/settings/numbering/{entity} — return settings for one entity
        /// </summary>
        [HttpGet("{entity}")]
        public async Task<IActionResult> GetSettings(string entity)
        {
            try
            {
                var settings = await _numberingService.GetSettingsAsync(entity);
                return Ok(new NumberingResponse
                {
                    Success = true,
                    Message = "Settings retrieved",
                    Data = settings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching numbering settings for {Entity}", entity);
                return StatusCode(500, new NumberingResponse { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// PUT /api/settings/numbering/{entity} — create or update settings for an entity
        /// </summary>
        [HttpPut("{entity}")]
        public async Task<IActionResult> UpdateSettings(string entity, [FromBody] UpdateNumberingSettingsRequest request)
        {
            try
            {
                var result = await _numberingService.SaveSettingsAsync(entity, request);
                return Ok(new NumberingResponse
                {
                    Success = true,
                    Message = $"Numbering settings for {entity} saved",
                    Data = result
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new NumberingResponse { Success = false, Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new NumberingResponse { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving numbering settings for {Entity}", entity);
                return StatusCode(500, new NumberingResponse { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// POST /api/numbering/preview — preview values from ad-hoc template (without saving)
        /// </summary>
        [HttpPost("/api/numbering/preview")]
        public IActionResult Preview([FromBody] NumberingPreviewRequest request)
        {
            try
            {
                var (isValid, errors, warnings) = _numberingService.ValidateTemplate(request.Template, request.Strategy);

                if (!isValid)
                {
                    return BadRequest(new NumberingPreviewResponse
                    {
                        Success = false,
                        Message = "Invalid template",
                        Warnings = errors
                    });
                }

                var preview = _numberingService.PreviewFromTemplate(request);

                return Ok(new NumberingPreviewResponse
                {
                    Success = true,
                    Message = "Preview generated",
                    Preview = preview,
                    Warnings = warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating numbering preview");
                return StatusCode(500, new NumberingPreviewResponse { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// GET /api/numbering/next?entity=Offer — get next value (admin/testing only)
        /// </summary>
        [HttpGet("/api/numbering/next")]
        public async Task<IActionResult> GetNext([FromQuery] string entity, [FromQuery] bool consume = false)
        {
            try
            {
                if (consume)
                {
                    var value = await _numberingService.GetNextAsync(entity);
                    return Ok(new NumberingResponse { Success = true, Message = "Next value consumed", Data = value });
                }
                else
                {
                    var preview = await _numberingService.PreviewAsync(entity, 1);
                    return Ok(new NumberingResponse { Success = true, Message = "Next value (preview only)", Data = preview.FirstOrDefault() });
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new NumberingResponse { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next numbering value for {Entity}", entity);
                return StatusCode(500, new NumberingResponse { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// POST /api/settings/numbering/validate — validate a template
        /// </summary>
        [HttpPost("validate")]
        public IActionResult Validate([FromBody] NumberingPreviewRequest request)
        {
            var (isValid, errors, warnings) = _numberingService.ValidateTemplate(request.Template, request.Strategy);
            return Ok(new
            {
                success = true,
                isValid,
                errors,
                warnings
            });
        }
    }
}
