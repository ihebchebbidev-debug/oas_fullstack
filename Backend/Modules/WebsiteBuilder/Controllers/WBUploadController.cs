using MyApi.Modules.WebsiteBuilder.DTOs;
using MyApi.Modules.WebsiteBuilder.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Security.Claims;

namespace MyApi.Modules.WebsiteBuilder.Controllers
{
    /// <summary>
    /// Website Builder file upload controller.
    /// Saves files to the local disk under ../uploads/wb_uploads/{folder}/
    /// following the same pattern as the Documents module.
    /// Stores metadata + file path in WB_Media.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WBUploadController : ControllerBase
    {
        private readonly IWBMediaService _mediaService;
        private readonly ILogger<WBUploadController> _logger;
        private readonly IWebHostEnvironment _env;

        // Max file size: 16MB
        private const long MaxFileSize = 16 * 1024 * 1024;

        // Allowed MIME types for website builder
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml", "image/avif",
            // Documents
            "application/pdf",
            // Video
            "video/mp4", "video/webm",
            // Audio
            "audio/mpeg", "audio/wav", "audio/ogg",
            // Fonts
            "font/woff", "font/woff2", "application/font-woff", "application/font-woff2"
        };

        // Compressible file types (text-based, SVG, etc.)
        private static readonly HashSet<string> CompressibleContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/svg+xml", "application/pdf"
        };

        public WBUploadController(
            IWBMediaService mediaService,
            ILogger<WBUploadController> logger,
            IWebHostEnvironment env)
        {
            _mediaService = mediaService;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Get the uploads root folder (one level above the backend folder),
        /// same as the Documents module.
        /// </summary>
        private string GetUploadsRoot()
        {
            var backendRoot = _env.ContentRootPath;
            var parentDir = Directory.GetParent(backendRoot)?.FullName ?? backendRoot;
            var uploadsDir = Path.Combine(parentDir, "uploads", "wb_uploads");

            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
                _logger.LogInformation("Created WB uploads directory at: {Path}", uploadsDir);
            }

            return uploadsDir;
        }

        /// <summary>
        /// Resolve a DB FilePath to an absolute disk path.
        /// FilePath is stored as "/uploads/wb_uploads/folder/file.ext".
        /// </summary>
        private string ResolveFilePath(string dbFilePath)
        {
            var relative = dbFilePath.TrimStart('/');
            if (relative.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                relative = relative.Substring("uploads/".Length);
            if (relative.StartsWith("wb_uploads/", StringComparison.OrdinalIgnoreCase))
                relative = relative.Substring("wb_uploads/".Length);

            return Path.Combine(GetUploadsRoot(), relative);
        }

        /// <summary>
        /// Upload a single file for the Website Builder.
        /// Saves to local disk under ../uploads/wb_uploads/{folder}/
        /// and stores metadata in WB_Media. Returns the full media record
        /// including a FileUrl pointing to the download endpoint.
        /// </summary>
        [HttpPost("file")]
        [RequestSizeLimit(16 * 1024 * 1024)]
        public async Task<ActionResult<WBUploadResponseDto>> UploadFile(
            IFormFile file,
            [FromQuery] int? siteId = null,
            [FromQuery] string? folder = null,
            [FromQuery] string? altText = null)
        {
            // Validate file before delegating
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            if (file.Length > MaxFileSize)
                return BadRequest(new { error = $"File size exceeds {MaxFileSize / (1024 * 1024)}MB limit" });

            var contentType = file.ContentType ?? "application/octet-stream";
            if (!AllowedContentTypes.Contains(contentType))
                return BadRequest(new { error = $"File type '{contentType}' is not allowed for website builder uploads" });

            _logger.LogInformation("WB Upload: Starting upload for {FileName}, Size: {Size}, SiteId: {SiteId}",
                file.FileName, file.Length, siteId);

            var result = await UploadSingleFileInternal(file, siteId, folder, altText);

            if (!result.Success)
            {
                _logger.LogError("WB Upload: Failed for {FileName}: {Error}", file.FileName, result.Error);
                return StatusCode(500, new { error = result.Error ?? "An error occurred while uploading the file" });
            }

            return Ok(result);
        }

        /// <summary>
        /// Upload multiple files at once for the Website Builder.
        /// </summary>
        [HttpPost("files")]
        [RequestSizeLimit(160 * 1024 * 1024)] // 10 files * 16MB
        public async Task<ActionResult<WBUploadMultipleResponseDto>> UploadFiles(
            [FromForm] List<IFormFile> files,
            [FromQuery] int? siteId = null,
            [FromQuery] string? folder = null)
        {
            try
            {
                if (files == null || files.Count == 0)
                    return BadRequest(new { error = "No files provided" });

                if (files.Count > 10)
                    return BadRequest(new { error = "Maximum 10 files allowed per upload" });

                // Validate all files first
                foreach (var file in files)
                {
                    if (file.Length > MaxFileSize)
                        return BadRequest(new { error = $"File '{file.FileName}' exceeds {MaxFileSize / (1024 * 1024)}MB limit" });

                    var ct = file.ContentType ?? "application/octet-stream";
                    if (!AllowedContentTypes.Contains(ct))
                        return BadRequest(new { error = $"File type '{ct}' for '{file.FileName}' is not allowed" });
                }

                _logger.LogInformation("WB Upload: Batch uploading {Count} files, SiteId: {SiteId}", files.Count, siteId);

                var results = new List<WBUploadResponseDto>();

                foreach (var file in files)
                {
                    // Reuse single-file upload logic via internal method
                    var result = await UploadSingleFileInternal(file, siteId, folder, null);
                    results.Add(result);
                }

                return Ok(new WBUploadMultipleResponseDto
                {
                    Results = results,
                    SuccessCount = results.Count(r => r.Success),
                    FailedCount = results.Count(r => !r.Success)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WB Upload: Error batch uploading files");
                return StatusCode(500, new { error = "An error occurred while uploading files" });
            }
        }

        /// <summary>
        /// GET /api/WBUpload/file/{mediaId} — Download/serve a WB media file.
        /// Auto-decompresses gzipped files on-the-fly.
        /// </summary>
        [HttpGet("file/{mediaId}")]
        [AllowAnonymous] // Public so published sites can reference images
        public async Task<ActionResult> ServeFile(int mediaId)
        {
            try
            {
                // Use the public lookup: it bypasses the tenant filter (anonymous
                // = no tenant context) but enforces that the owning site is
                // currently Published. Prevents cross-tenant file disclosure by
                // simply guessing a numeric mediaId.
                var media = await _mediaService.GetPublicMediaByIdAsync(mediaId);
                if (media == null)
                    return NotFound(new { error = "File not found" });

                var fullPath = ResolveFilePath(media.FilePath);

                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning("WB Upload: File not found on disk: {Path} (DB Id={Id})", fullPath, mediaId);
                    return NotFound(new { error = "File not found on server" });
                }

                var contentType = media.ContentType ?? "application/octet-stream";

                // If compressed (.gz), decompress on-the-fly
                if (fullPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                        var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress, leaveOpen: false);
                        return File(gzipStream, contentType, media.OriginalName ?? media.FileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "WB Upload: Error decompressing file {Id}", mediaId);
                        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                        return File(stream, contentType, media.OriginalName ?? media.FileName);
                    }
                }

                // Serve uncompressed file
                var uncompressedStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);

                // For images, serve inline (browser displays directly)
                if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return File(uncompressedStream, contentType);
                }

                return File(uncompressedStream, contentType, media.OriginalName ?? media.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WB Upload: Error serving file {MediaId}", mediaId);
                return StatusCode(500, new { error = "Error serving file" });
            }
        }

        /// <summary>
        /// Delete a media file. Removes from disk AND soft-deletes from WB_Media.
        /// </summary>
        [HttpDelete("{mediaId}")]
        public async Task<ActionResult> DeleteMedia(int mediaId)
        {
            try
            {
                var media = await _mediaService.GetMediaByIdInternalAsync(mediaId);
                if (media == null)
                    return NotFound(new { error = $"Media with ID {mediaId} not found" });

                // Delete physical file from disk
                var fullPath = ResolveFilePath(media.FilePath);
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    _logger.LogInformation("WB Upload: Deleted file from disk: {Path}", fullPath);
                }

                // Soft-delete from database
                var deleted = await _mediaService.DeleteMediaAsync(mediaId);
                if (!deleted)
                    return NotFound(new { error = $"Media with ID {mediaId} not found in database" });

                _logger.LogInformation("WB Upload: Media {MediaId} deleted from disk and database", mediaId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WB Upload: Error deleting media {MediaId}", mediaId);
                return StatusCode(500, new { error = "An error occurred while deleting the file" });
            }
        }

        // ── Internal helpers ──

        private async Task<WBUploadResponseDto> UploadSingleFileInternal(
            IFormFile file, int? siteId, string? folder, string? altText)
        {
            try
            {
                var contentType = file.ContentType ?? "application/octet-stream";

                // ── Magic-byte signature validation ──
                // Client-supplied Content-Type cannot be trusted. Verify the actual
                // file bytes match a known signature for the claimed type. Prevents
                // executables masquerading as images/PDFs.
                if (!await IsValidFileSignatureAsync(file, contentType))
                {
                    return new WBUploadResponseDto
                    {
                        Success = false,
                        Error = $"File content does not match declared type '{contentType}'."
                    };
                }

                var subFolder = folder ?? "general";
                var targetDir = Path.Combine(GetUploadsRoot(), subFolder);
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                var safeFileName = SanitizeFileName(file.FileName);
                var uniqueFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid().ToString("N")[..8]}_{safeFileName}";
                var diskPath = Path.Combine(targetDir, uniqueFileName);

                await using (var stream = new FileStream(diskPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/uploads/wb_uploads/{subFolder}/{uniqueFileName}";
                var actualFileSize = file.Length;

                // Compress if applicable
                if (CompressibleContentTypes.Contains(contentType))
                {
                    try
                    {
                        var compressedPath = diskPath + ".gz";
                        await using (var sourceStream = new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                        await using (var targetStream = new FileStream(compressedPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        await using (var gzipStream = new GZipStream(targetStream, CompressionMode.Compress, leaveOpen: true))
                        {
                            await sourceStream.CopyToAsync(gzipStream, 81920);
                        }
                        System.IO.File.Delete(diskPath);
                        relativePath += ".gz";
                        actualFileSize = new FileInfo(compressedPath).Length;
                    }
                    catch { /* Use uncompressed */ }
                }

                var createDto = new CreateWBMediaRequestDto
                {
                    SiteId = siteId,
                    FileName = safeFileName,
                    OriginalName = file.FileName,
                    FilePath = relativePath,
                    FileUrl = "",
                    FileSize = actualFileSize,
                    ContentType = contentType,
                    Folder = subFolder,
                    AltText = altText
                };

                var currentUser = GetCurrentUser();
                var mediaRecord = await _mediaService.CreateMediaAsync(createDto, currentUser);

                var downloadUrl = $"/api/WBUpload/file/{mediaRecord.Id}";
                await _mediaService.UpdateFileUrlAsync(mediaRecord.Id, downloadUrl);
                mediaRecord.FileUrl = downloadUrl;

                return new WBUploadResponseDto { Success = true, Media = mediaRecord };
            }
            catch (Exception ex)
            {
                return new WBUploadResponseDto
                {
                    Success = false,
                    Error = $"Upload failed for {file.FileName}: {ex.Message}"
                };
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            while (sanitized.Contains("__"))
                sanitized = sanitized.Replace("__", "_");
            return sanitized.Trim('_');
        }

        private string GetCurrentUser()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ??
                   User.FindFirst(ClaimTypes.Name)?.Value ??
                   User.FindFirst("email")?.Value ??
                   "system";
        }

        /// <summary>
        /// Magic-byte / file-signature validation. The browser-supplied
        /// Content-Type header is trivially spoofable; this checks the actual
        /// leading bytes of the file. SVG additionally gets a script-tag scan
        /// to prevent stored-XSS via uploaded <see langword="image/svg+xml" />.
        /// </summary>
        private static async Task<bool> IsValidFileSignatureAsync(IFormFile file, string contentType)
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var buffer = new byte[16];
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read == 0) return false;
                var head = buffer.AsMemory(0, read);

                bool StartsWith(params byte[] sig) => head.Span.Length >= sig.Length && head.Span.Slice(0, sig.Length).SequenceEqual(sig);

                var ct = contentType.ToLowerInvariant();
                var ok = ct switch
                {
                    "image/jpeg" => StartsWith(0xFF, 0xD8, 0xFF),
                    "image/png" => StartsWith(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
                    "image/gif" => StartsWith(0x47, 0x49, 0x46, 0x38), // GIF8
                    "image/webp" => StartsWith(0x52, 0x49, 0x46, 0x46) // RIFF....WEBP
                                    && head.Span.Length >= 12
                                    && head.Span.Slice(8, 4).SequenceEqual(new byte[] { 0x57, 0x45, 0x42, 0x50 }),
                    "image/avif" => head.Span.Length >= 12
                                    && head.Span.Slice(4, 4).SequenceEqual(new byte[] { 0x66, 0x74, 0x79, 0x70 }), // ftyp
                    "image/svg+xml" => true, // validated separately below (text-based)
                    "application/pdf" => StartsWith(0x25, 0x50, 0x44, 0x46), // %PDF
                    "video/mp4" => head.Span.Length >= 12
                                    && head.Span.Slice(4, 4).SequenceEqual(new byte[] { 0x66, 0x74, 0x79, 0x70 }),
                    "video/webm" => StartsWith(0x1A, 0x45, 0xDF, 0xA3),
                    "audio/mpeg" => StartsWith(0xFF, 0xFB) || StartsWith(0xFF, 0xF3) || StartsWith(0xFF, 0xF2) || StartsWith(0x49, 0x44, 0x33),
                    "audio/wav" => StartsWith(0x52, 0x49, 0x46, 0x46)
                                    && head.Span.Length >= 12
                                    && head.Span.Slice(8, 4).SequenceEqual(new byte[] { 0x57, 0x41, 0x56, 0x45 }),
                    "audio/ogg" => StartsWith(0x4F, 0x67, 0x67, 0x53),
                    "font/woff" or "application/font-woff" => StartsWith(0x77, 0x4F, 0x46, 0x46),
                    "font/woff2" or "application/font-woff2" => StartsWith(0x77, 0x4F, 0x46, 0x32),
                    _ => false
                };

                if (!ok) return false;

                // SVG sanity check — reject embedded scripts / handlers / external entities.
                if (ct == "image/svg+xml")
                {
                    stream.Position = 0;
                    using var reader = new StreamReader(stream, leaveOpen: false);
                    var content = await reader.ReadToEndAsync();
                    var lower = content.ToLowerInvariant();
                    if (lower.Contains("<script") ||
                        lower.Contains("javascript:") ||
                        lower.Contains("<!entity") ||
                        System.Text.RegularExpressions.Regex.IsMatch(lower, @"on\w+\s*="))
                    {
                        return false;
                    }
                    if (!lower.Contains("<svg")) return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    // ── Upload-specific DTOs ──

    public class WBUploadResponseDto
    {
        public bool Success { get; set; }
        public WBMediaResponseDto? Media { get; set; }
        public string? Error { get; set; }
    }

    public class WBUploadMultipleResponseDto
    {
        public List<WBUploadResponseDto> Results { get; set; } = new();
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
    }
}