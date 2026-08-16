using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyApi.Modules.Shared.Services
{
    public interface IUploadThingService
    {
        Task<UploadThingResult> UploadFileAsync(IFormFile file);
        Task<List<UploadThingResult>> UploadFilesAsync(IEnumerable<IFormFile> files);
        Task<bool> DeleteFileAsync(string fileKey);
        bool IsConfigured { get; }
    }

    public class UploadThingResult
    {
        public bool Success { get; set; }
        public string? FileUrl { get; set; }
        public string? FileKey { get; set; }
        public string? FileName { get; set; }
        public long FileSize { get; set; }
        public string? ContentType { get; set; }
        public string? Error { get; set; }
    }

    public class UploadThingService : IUploadThingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UploadThingService> _logger;
        private readonly string _apiKey;
        private readonly string _appId;
        private readonly string _region;

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        public UploadThingService(
            IHttpClientFactory httpClientFactory, 
            IConfiguration config, 
            ILogger<UploadThingService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;

            var token = config["UploadThing:Token"] ?? Environment.GetEnvironmentVariable("UPLOADTHING_TOKEN");
            
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("UploadThing: No token found");
                _apiKey = "";
                _appId = "";
                _region = "fra1"; // default
                return;
            }

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = JsonSerializer.Deserialize<TokenData>(json, options);
                _apiKey = data?.ApiKey ?? "";
                _appId = data?.AppId ?? "";
                _region = data?.Regions?.FirstOrDefault() ?? "fra1";
                _logger.LogInformation("UploadThing: Connected! AppId: {AppId}, Region: {Region}", _appId, _region);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadThing: Failed to decode token");
                _apiKey = "";
                _appId = "";
                _region = "fra1";
            }
        }

        public async Task<UploadThingResult> UploadFileAsync(IFormFile file)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                    return new UploadThingResult { Success = false, Error = "UploadThing API key not configured" };

                _logger.LogInformation("UploadThing: Starting upload for {FileName}, Size: {Size}", 
                    file.FileName, file.Length);

                // Generate a unique file key
                var fileKey = Guid.NewGuid().ToString("N");
                
                // Get file extension
                var ext = Path.GetExtension(file.FileName);
                if (!string.IsNullOrEmpty(ext))
                {
                    fileKey += ext;
                }

                // ===== METHOD 1: Use v7 Direct Signed URL Upload =====
                // Build the signed ingest URL
                var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
                var contentType = file.ContentType ?? "application/octet-stream";
                
                // Build query parameters
                var queryParams = new Dictionary<string, string>
                {
                    ["expires"] = expires.ToString(),
                    ["x-ut-identifier"] = _appId,
                    ["x-ut-file-name"] = file.FileName,
                    ["x-ut-file-size"] = file.Length.ToString(),
                    ["x-ut-file-type"] = contentType,
                    ["x-ut-slug"] = "serverUpload", // Generic route for server-side uploads
                    ["x-ut-content-disposition"] = "inline"
                };

                // Build the URL without signature
                var regionAlias = GetRegionAlias(_region);
                var baseUrl = $"https://{regionAlias}.ingest.uploadthing.com/{fileKey}";
                var queryString = string.Join("&", queryParams.Select(kv => 
                    $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
                var urlWithParams = $"{baseUrl}?{queryString}";

                // Generate HMAC-SHA256 signature
                var signature = GenerateSignature(urlWithParams, _apiKey);
                var signedUrl = $"{urlWithParams}&signature=hmac-sha256%3D{signature}";

                _logger.LogInformation("UploadThing: Using signed URL upload to {Region}", regionAlias);

                // Upload the file with PUT request
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                var fileBytes = stream.ToArray();

                using var uploadReq = new HttpRequestMessage(HttpMethod.Put, signedUrl);
                uploadReq.Content = new ByteArrayContent(fileBytes);
                uploadReq.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                uploadReq.Content.Headers.ContentLength = fileBytes.Length;

                var uploadResp = await _httpClient.SendAsync(uploadReq);
                var uploadRespText = await uploadResp.Content.ReadAsStringAsync();

                _logger.LogInformation("UploadThing: Direct upload response: {Status} - {Response}", 
                    uploadResp.StatusCode, uploadRespText?.Substring(0, Math.Min(200, uploadRespText?.Length ?? 0)));

                if (uploadResp.IsSuccessStatusCode || 
                    uploadResp.StatusCode == System.Net.HttpStatusCode.OK ||
                    uploadResp.StatusCode == System.Net.HttpStatusCode.Created ||
                    uploadResp.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    var finalUrl = $"https://utfs.io/f/{fileKey}";
                    _logger.LogInformation("UploadThing: Success! URL: {Url}", finalUrl);

                    return new UploadThingResult
                    {
                        Success = true,
                        FileUrl = finalUrl,
                        FileKey = fileKey,
                        FileName = file.FileName,
                        FileSize = file.Length,
                        ContentType = contentType
                    };
                }

                // If signed URL fails, try the v6 API as fallback
                _logger.LogWarning("UploadThing: Signed URL upload failed, trying v6 API fallback");
                return await UploadViaV6ApiAsync(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadThing: Error uploading file");
                return new UploadThingResult { Success = false, Error = ex.Message };
            }
        }

        private async Task<UploadThingResult> UploadViaV6ApiAsync(IFormFile file)
        {
            try
            {
                // Try v6 API endpoint for presigned URLs
                var requestBody = new
                {
                    files = new[] 
                    {
                        new {
                            name = file.FileName,
                            size = file.Length,
                            type = file.ContentType ?? "application/octet-stream"
                        }
                    },
                    acl = "public-read",
                    contentDisposition = "inline"
                };

                var requestJson = JsonSerializer.Serialize(requestBody);
                _logger.LogInformation("UploadThing v6: Request to uploadFiles");

                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.uploadthing.com/v6/uploadFiles");
                req.Headers.Add("x-uploadthing-api-key", _apiKey);
                req.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(req);
                var responseText = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("UploadThing v6: Response: {Status} - {Response}", 
                    response.StatusCode, responseText?.Substring(0, Math.Min(500, responseText?.Length ?? 0)));

                if (!response.IsSuccessStatusCode)
                    return new UploadThingResult { Success = false, Error = $"v6 API failed: {responseText}" };

                var presigned = JsonSerializer.Deserialize<V6PresignedResponse>(responseText ?? "{}", 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (presigned?.Data == null || presigned.Data.Length == 0)
                    return new UploadThingResult { Success = false, Error = "No upload data from v6 API" };

                var info = presigned.Data[0];
                _logger.LogInformation("UploadThing v6: Got presigned URL, Key: {Key}", info.Key);

                // Upload using the presigned URL and fields
                using var content = new MultipartFormDataContent();
                
                if (info.Fields != null)
                {
                    foreach (var field in info.Fields)
                    {
                        content.Add(new StringContent(field.Value ?? ""), field.Key);
                    }
                }

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                var fileContent = new ByteArrayContent(stream.ToArray());
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
                content.Add(fileContent, "file", file.FileName);

                var uploadResp = await _httpClient.PostAsync(info.Url, content);
                
                if (uploadResp.IsSuccessStatusCode || 
                    uploadResp.StatusCode == System.Net.HttpStatusCode.NoContent ||
                    uploadResp.StatusCode == System.Net.HttpStatusCode.Created)
                {
                    var finalUrl = $"https://utfs.io/f/{info.Key}";
                    return new UploadThingResult
                    {
                        Success = true,
                        FileUrl = finalUrl,
                        FileKey = info.Key,
                        FileName = file.FileName,
                        FileSize = file.Length,
                        ContentType = file.ContentType
                    };
                }

                var uploadError = await uploadResp.Content.ReadAsStringAsync();
                return new UploadThingResult { Success = false, Error = $"v6 upload failed: {uploadError}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadThing v6: Error");
                return new UploadThingResult { Success = false, Error = $"v6 fallback failed: {ex.Message}" };
            }
        }

        private string GetRegionAlias(string region)
        {
            // Map region codes to ingest aliases
            return region?.ToLower() switch
            {
                "fra1" => "fra1",
                "sfo1" => "sfo1",
                "sea1" => "sea1",
                "iad1" => "iad1",
                _ => "fra1"
            };
        }

        private static string GenerateSignature(string url, string apiKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(url));
            return Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        public async Task<List<UploadThingResult>> UploadFilesAsync(IEnumerable<IFormFile> files)
        {
            var tasks = files.Select(UploadFileAsync);
            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        public async Task<bool> DeleteFileAsync(string fileKey)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    _logger.LogError("UploadThing API key not configured");
                    return false;
                }

                var requestBody = new { fileKeys = new[] { fileKey } };
                var json = JsonSerializer.Serialize(requestBody);

                // Use v6 API for delete
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.uploadthing.com/v6/deleteFiles");
                req.Headers.Add("x-uploadthing-api-key", _apiKey);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _httpClient.SendAsync(req);
                var respText = await resp.Content.ReadAsStringAsync();
                
                _logger.LogInformation("UploadThing: Delete response: {Status} - {Response}", resp.StatusCode, respText);
                
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadThing: Error deleting file");
                return false;
            }
        }

        private class TokenData
        {
            public string? ApiKey { get; set; }
            public string? AppId { get; set; }
            public string[]? Regions { get; set; }
        }

        private class V6PresignedResponse
        {
            public V6PresignedFile[]? Data { get; set; }
        }

        private class V6PresignedFile
        {
            public string? Url { get; set; }
            public string? Key { get; set; }
            public Dictionary<string, string>? Fields { get; set; }
            public string? FileUrl { get; set; }
        }
    }
}
