using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.DynamicForms.DTOs;
using MyApi.Modules.DynamicForms.Services;

namespace MyApi.Modules.DynamicForms.Controllers
{
    /// <summary>
    /// Public API Controller for accessing shared forms without authentication
    /// </summary>
    [ApiController]
    [Route("api/public/forms")]
    public class PublicFormsController : ControllerBase
    {
        private readonly IDynamicFormService _formService;
        private readonly ILogger<PublicFormsController> _logger;

        public PublicFormsController(
            IDynamicFormService formService,
            ILogger<PublicFormsController> logger)
        {
            _formService = formService;
            _logger = logger;
        }

        /// <summary>
        /// Get a public form by its slug
        /// </summary>
        [HttpGet("{slug}")]
        public async Task<ActionResult<DynamicFormDto>> GetBySlug(string slug)
        {
            try
            {
                var form = await _formService.GetBySlugAsync(slug);
                if (form == null)
                {
                    return NotFound(new { message = "Form not found or not publicly available" });
                }
                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching public form {Slug}", slug);
                return StatusCode(500, new { message = "Error fetching form" });
            }
        }

        /// <summary>
        /// Submit a response to a public form
        /// </summary>
        [HttpPost("{slug}/responses")]
        public async Task<ActionResult<DynamicFormResponseDto>> SubmitResponse(string slug, [FromBody] PublicSubmitFormResponseDto dto)
        {
            try
            {
                var response = await _formService.SubmitPublicResponseAsync(slug, dto);
                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Form not found or not publicly available" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting response to public form {Slug}", slug);
                return StatusCode(500, new { message = "Error submitting response" });
            }
        }
    }
}
