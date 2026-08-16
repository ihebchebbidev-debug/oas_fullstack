using MyApi.Modules.Preferences.DTOs;
using MyApi.Modules.Preferences.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyApi.Modules.Preferences.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // PDF settings are tenant-scoped rows: an anonymous caller could otherwise
    // read or overwrite another company's report configuration.
    [Authorize]
    public class PdfSettingsController : ControllerBase
    {
        private readonly IPdfSettingsService _pdfSettingsService;
        private readonly ILogger<PdfSettingsController> _logger;

        public PdfSettingsController(IPdfSettingsService pdfSettingsService, ILogger<PdfSettingsController> logger)
        {
            _pdfSettingsService = pdfSettingsService;
            _logger = logger;
        }

        /// <summary>
        /// Get all PDF settings for all modules
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllSettings()
        {
            try
            {
                var settings = await _pdfSettingsService.GetAllSettingsAsync();
                return Ok(new PdfSettingsListResponse
                {
                    Success = true,
                    Message = "PDF settings retrieved successfully",
                    Data = settings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all PDF settings");
                return StatusCode(500, new PdfSettingsResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        /// <summary>
        /// Get PDF settings for a specific module
        /// </summary>
        [HttpGet("{module}")]
        public async Task<IActionResult> GetSettingsByModule(string module)
        {
            try
            {
                if (string.IsNullOrEmpty(module))
                {
                    return BadRequest(new PdfSettingsResponse
                    {
                        Success = false,
                        Message = "Module name is required"
                    });
                }

                var settings = await _pdfSettingsService.GetSettingsByModuleAsync(module);
                
                if (settings == null)
                {
                    // Return empty settings instead of 404 for new modules
                    return Ok(new PdfSettingsResponse
                    {
                        Success = true,
                        Message = "No settings found for this module",
                        Data = new PdfSettingsDto
                        {
                            Module = module,
                            SettingsJson = new { },
                            UpdatedAt = DateTime.UtcNow
                        }
                    });
                }

                return Ok(new PdfSettingsResponse
                {
                    Success = true,
                    Message = "PDF settings retrieved successfully",
                    Data = settings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting PDF settings for module {Module}", module);
                return StatusCode(500, new PdfSettingsResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        /// <summary>
        /// Create or update PDF settings for a module
        /// </summary>
        [HttpPut("{module}")]
        public async Task<IActionResult> UpdateSettings(string module, [FromBody] UpdatePdfSettingsRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(module))
                {
                    return BadRequest(new PdfSettingsResponse
                    {
                        Success = false,
                        Message = "Module name is required"
                    });
                }

                var settings = await _pdfSettingsService.CreateOrUpdateSettingsAsync(module, request.SettingsJson);

                return Ok(new PdfSettingsResponse
                {
                    Success = true,
                    Message = "PDF settings updated successfully",
                    Data = settings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating PDF settings for module {Module}", module);
                return StatusCode(500, new PdfSettingsResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        /// <summary>
        /// Create PDF settings for a module
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateSettings([FromBody] CreatePdfSettingsRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Module))
                {
                    return BadRequest(new PdfSettingsResponse
                    {
                        Success = false,
                        Message = "Module name is required"
                    });
                }

                var settings = await _pdfSettingsService.CreateOrUpdateSettingsAsync(request.Module, request.SettingsJson);

                return Ok(new PdfSettingsResponse
                {
                    Success = true,
                    Message = "PDF settings created successfully",
                    Data = settings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PDF settings");
                return StatusCode(500, new PdfSettingsResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        /// <summary>
        /// Delete PDF settings for a module (resets to default)
        /// </summary>
        [HttpDelete("{module}")]
        public async Task<IActionResult> DeleteSettings(string module)
        {
            try
            {
                if (string.IsNullOrEmpty(module))
                {
                    return BadRequest(new PdfSettingsResponse
                    {
                        Success = false,
                        Message = "Module name is required"
                    });
                }

                var success = await _pdfSettingsService.DeleteSettingsAsync(module);
                
                return Ok(new PdfSettingsResponse
                {
                    Success = true,
                    Message = success ? "PDF settings deleted successfully" : "No settings found for this module"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting PDF settings for module {Module}", module);
                return StatusCode(500, new PdfSettingsResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }
    }
}
