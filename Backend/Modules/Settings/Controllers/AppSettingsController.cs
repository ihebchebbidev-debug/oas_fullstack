using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Settings.DTOs;
using MyApi.Modules.Settings.Services;

namespace MyApi.Modules.Settings.Controllers
{
    [ApiController]
    [Route("api/settings/app")]
    [Authorize]
    public class AppSettingsController : ControllerBase
    {
        private readonly IAppSettingsService _appSettingsService;
        private readonly ILogger<AppSettingsController> _logger;

        public AppSettingsController(IAppSettingsService appSettingsService, ILogger<AppSettingsController> logger)
        {
            _appSettingsService = appSettingsService;
            _logger = logger;
        }

        /// <summary>
        /// Get all application settings
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var settings = await _appSettingsService.GetAllSettingsAsync();
            return Ok(new { success = true, data = settings });
        }

        /// <summary>
        /// Get a single application setting by key
        /// </summary>
        [HttpGet("{key}")]
        public async Task<IActionResult> GetByKey(string key)
        {
            var value = await _appSettingsService.GetSettingAsync(key);
            if (value == null)
                return NotFound(new { success = false, message = $"Setting '{key}' not found" });

            return Ok(new { success = true, data = new AppSettingDto { Key = key, Value = value } });
        }

        /// <summary>
        /// Update a single application setting (admin only)
        /// </summary>
        [HttpPut("{key}")]
        public async Task<IActionResult> Update(string key, [FromBody] UpdateAppSettingRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Value))
                return BadRequest(new { success = false, message = "Value is required" });

            var result = await _appSettingsService.SetSettingAsync(key, request.Value);
            return Ok(new { success = true, data = result });
        }
    }
}
