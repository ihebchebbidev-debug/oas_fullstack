using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Shared.DTOs;
using System.Text;

namespace MyApi.Modules.Dashboards.Controllers
{
    /// <summary>
    /// Proxy controller to fetch external API data on behalf of the frontend.
    /// Avoids CORS issues when widgets pull data from third-party APIs.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ExternalApiProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ExternalApiProxyController> _logger;

        public ExternalApiProxyController(
            IHttpClientFactory httpClientFactory,
            ILogger<ExternalApiProxyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public class ProxyRequest
        {
            public string Url { get; set; } = string.Empty;
            public string Method { get; set; } = "GET";
            public Dictionary<string, string>? Headers { get; set; }
            public string? Body { get; set; }
        }

        /// <summary>
        /// POST /api/ExternalApiProxy/fetch
        /// Proxies an HTTP request to an external API and returns the response.
        /// </summary>
        [HttpPost("fetch")]
        public async Task<IActionResult> FetchExternal([FromBody] ProxyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return BadRequest(ApiResponse<object>.ErrorResponse("URL is required"));

            // Basic URL validation
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid URL"));
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var httpRequest = new HttpRequestMessage(
                    request.Method?.ToUpper() == "POST" ? HttpMethod.Post : HttpMethod.Get,
                    request.Url
                );

                // Forward custom headers
                if (request.Headers != null)
                {
                    foreach (var kv in request.Headers)
                    {
                        httpRequest.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                    }
                }

                // Add body for POST
                if (request.Method?.ToUpper() == "POST" && !string.IsNullOrEmpty(request.Body))
                {
                    httpRequest.Content = new StringContent(request.Body, Encoding.UTF8, "application/json");
                }

                var response = await client.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("External API returned {StatusCode} for {Url}", (int)response.StatusCode, request.Url);
                }

                // Return raw JSON response
                return Content(content, "application/json");
            }
            catch (TaskCanceledException)
            {
                return StatusCode(504, ApiResponse<object>.ErrorResponse("External API request timed out"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to proxy request to {Url}", request.Url);
                return StatusCode(502, ApiResponse<object>.ErrorResponse($"Failed to reach external API: {ex.Message}"));
            }
        }
    }
}
