using Microsoft.AspNetCore.Mvc;
using MyApi.Configuration;

namespace MyApi.Modules.Shared.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DevController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DevController> _logger;

        public DevController(IConfiguration configuration, IWebHostEnvironment environment, ILogger<DevController> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        /// <summary>
        /// Generate a development JWT token for API testing (Development environment only)
        /// </summary>
        /// <param name="userId">Optional user ID (defaults to 1)</param>
        /// <param name="email">Optional email (defaults to dev@flowservice.com)</param>
        /// <returns>JWT token for development testing</returns>
        [HttpGet("token")]
        public ActionResult<object> GenerateDevToken([FromQuery] string userId = "1", [FromQuery] string email = "dev@flowservice.com")
        {
            // Only allow in development environment
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            try
            {
                var token = TokenHelper.GenerateDevelopmentToken(_configuration);
                
                _logger.LogInformation("Development token generated for testing");
                
                return Ok(new
                {
                    token = token,
                    type = "Bearer",
                    expiresIn = 86400, // 24 hours in seconds
                    usage = "Add 'Authorization: Bearer {token}' to your API requests",
                    note = "This token is only available in development environment"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating development token");
                return StatusCode(500, new { message = "Error generating development token" });
            }
        }

        /// <summary>
        /// Generate a permanent test token (Development environment only)
        /// </summary>
        /// <param name="userId">User ID for the token</param>
        /// <param name="email">Email for the token</param>
        /// <returns>Long-lived JWT token for testing</returns>
        [HttpGet("permanent-token")]
        public ActionResult<object> GeneratePermanentToken([FromQuery] string userId = "999", [FromQuery] string email = "test@flowservice.com")
        {
            // Only allow in development environment
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            try
            {
                var token = TokenHelper.GeneratePermanentTestToken(_configuration, userId, email);
                
                _logger.LogInformation("Permanent test token generated for user {UserId}", userId);
                
                return Ok(new
                {
                    token = token,
                    type = "Bearer",
                    userId = userId,
                    email = email,
                    expiresIn = 31536000, // 1 year in seconds
                    usage = "Add 'Authorization: Bearer {token}' to your API requests",
                    warning = "This is a long-lived token for testing purposes only. Do not use in production!",
                    copyToken = $"Bearer {token}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating permanent test token");
                return StatusCode(500, new { message = "Error generating permanent test token" });
            }
        }

        /// <summary>
        /// Get API documentation and testing information
        /// </summary>
        /// <returns>API testing information</returns>
        [HttpGet("info")]
        public ActionResult<object> GetApiInfo()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            
            return Ok(new
            {
                api = new
                {
                    title = "Flow Service API",
                    version = "v1.0.0",
                    baseUrl = baseUrl,
                    documentation = $"{baseUrl}/api-docs",
                    health = $"{baseUrl}/health"
                },
                authentication = new
                {
                    type = "JWT Bearer Token",
                    header = "Authorization: Bearer {token}",
                    devToken = _environment.IsDevelopment() ? $"{baseUrl}/api/dev/token" : "Not available in production",
                    permanentToken = _environment.IsDevelopment() ? $"{baseUrl}/api/dev/permanent-token" : "Not available in production"
                },
                endpoints = new
                {
                    auth = $"{baseUrl}/api/auth",
                    users = $"{baseUrl}/api/users",
                    contacts = $"{baseUrl}/api/contacts",
                    roles = $"{baseUrl}/api/roles",
                    skills = $"{baseUrl}/api/skills",
                    articles = $"{baseUrl}/api/articles",
                    calendar = $"{baseUrl}/api/calendar",
                    lookups = $"{baseUrl}/api/lookups"
                },
                testing = new
                {
                    swagger = $"{baseUrl}/api-docs",
                    postmanCollection = "Available in Swagger UI export",
                    sampleRequests = "Check Swagger documentation for detailed examples"
                }
            });
        }
    }
}
