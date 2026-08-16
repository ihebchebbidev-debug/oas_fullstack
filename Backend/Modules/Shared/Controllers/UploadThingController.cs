using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyApi.Modules.Shared.Controllers
{
    /// <summary>
    /// UploadThing v7 API Controller
    /// Provides /api/uploadthing/prepare endpoint for frontend to get presigned URLs
    /// Frontend then uploads directly to S3 using the presigned URL
    /// </summary>
    [ApiController]
    [Route("api/uploadthing")]
    [Authorize]
    public class UploadThingController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UploadThingController> _logger;
        private readonly string _apiKey;

        public UploadThingController(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<UploadThingController> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;

            // Get API key - supports both raw API key (sk_live_...) and base64 token formats
            var token = config["UploadThing:Token"] ?? Environment.GetEnvironmentVariable("UPLOADTHING_TOKEN");
            var rawApiKey = config["UploadThing:ApiKey"] ?? Environment.GetEnvironmentVariable("UPLOADTHING_API_KEY");
            
            // First check for raw API key
            if (!string.IsNullOrEmpty(rawApiKey) && rawApiKey.StartsWith("sk_"))
            {
                _apiKey = rawApiKey;
                _logger.LogInformation("UploadThing: Using raw API key from UPLOADTHING_API_KEY");
            }
            // Then check if token is a raw API key (starts with sk_)
            else if (!string.IsNullOrEmpty(token) && token.StartsWith("sk_"))
            {
                _apiKey = token;
                _logger.LogInformation("UploadThing: Using raw API key from UPLOADTHING_TOKEN");
            }
            // Otherwise try to decode as base64 JSON token
            else if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<TokenData>(json, options);
                    _apiKey = data?.ApiKey ?? "";
                    if (!string.IsNullOrEmpty(_apiKey))
                    {
                        _logger.LogInformation("UploadThing: Extracted API key from base64 token");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("UploadThing: Failed to decode base64 token: {Error}", ex.Message);
                    _apiKey = "";
                }
            }
            else
            {
                _apiKey = "";
                _logger.LogWarning("UploadThing: No API key or token configured");
            }
        }

        /// <summary>
        /// Prepare upload - calls UploadThing v6 API to get presigned URL
        /// Frontend calls this, then uploads directly to S3
        /// </summary>
        [HttpPost("prepare")]
        public async Task<IActionResult> PrepareUpload([FromBody] PrepareUploadRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    _logger.LogError("UploadThing API key not configured");
                    return StatusCode(500, new { error = "UploadThing API key not configured on server" });
                }

                _logger.LogInformation("UploadThing Prepare: {FileName}, Size: {Size}, Type: {Type}", 
                    request.FileName, request.FileSize, request.FileType);

                // Call UploadThing v7 API to get presigned URL
                // v7 uses /v7/prepareUpload with fileName, fileSize, fileType at root level
                var payload = new
                {
                    fileName = request.FileName,
                    fileSize = request.FileSize,
                    fileType = request.FileType ?? "application/octet-stream",
                    acl = "public-read",
                    contentDisposition = "inline"
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                _logger.LogInformation("UploadThing v7 Request: {Payload}", jsonPayload);

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.uploadthing.com/v7/prepareUpload");
                httpRequest.Headers.Add("x-uploadthing-api-key", _apiKey);
                httpRequest.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                var responseText = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("UploadThing v7 Response: {Status} - {Response}", 
                    response.StatusCode, responseText?.Substring(0, Math.Min(500, responseText?.Length ?? 0)));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("UploadThing v7 API failed: {Response}", responseText);
                    return StatusCode((int)response.StatusCode, new { error = responseText });
                }

                // Parse the response - v7 returns object directly with url, key, fileUrl, fields
                var uploadData = JsonSerializer.Deserialize<V7FileData>(responseText ?? "{}", 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (uploadData == null || string.IsNullOrEmpty(uploadData.Url))
                {
                    return StatusCode(500, new { error = "No upload data returned from UploadThing" });
                }
                
                // Return the presigned URL info to frontend
                return Ok(new PrepareUploadResponse
                {
                    PresignedUrl = uploadData.Url ?? "",
                    Key = uploadData.Key ?? "",
                    FileKey = uploadData.Key ?? "",
                    FileUrl = uploadData.FileUrl ?? $"https://utfs.io/f/{uploadData.Key}",
                    Fields = uploadData.Fields ?? new Dictionary<string, string>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing upload");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Delete files from UploadThing
        /// </summary>
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteFiles([FromBody] DeleteFilesRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    return StatusCode(500, new { error = "UploadThing API key not configured" });
                }

                var payload = JsonSerializer.Serialize(new { fileKeys = request.FileKeys });

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.uploadthing.com/v7/deleteFiles");
                httpRequest.Headers.Add("x-uploadthing-api-key", _apiKey);
                httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);

                if (response.IsSuccessStatusCode)
                {
                    return Ok(new { success = true });
                }

                var errorText = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new { error = errorText });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting files");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #region DTOs

        private class TokenData
        {
            public string? ApiKey { get; set; }
            public string? AppId { get; set; }
            public string[]? Regions { get; set; }
        }

        // v7 returns object directly, not wrapped in data array
        private class V7FileData
        {
            public string? Url { get; set; }
            public string? Key { get; set; }
            public string? FileUrl { get; set; }
            public string? PollingUrl { get; set; }
            public Dictionary<string, string>? Fields { get; set; }
        }

        #endregion
    }

    #region Request/Response DTOs

    public class PrepareUploadRequest
    {
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public string? FileType { get; set; }
    }

    public class PrepareUploadResponse
    {
        public string PresignedUrl { get; set; } = "";
        public string Key { get; set; } = "";
        public string FileKey { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public Dictionary<string, string> Fields { get; set; } = new();
    }

    public class DeleteFilesRequest
    {
        public string[] FileKeys { get; set; } = Array.Empty<string>();
    }

    #endregion
}
