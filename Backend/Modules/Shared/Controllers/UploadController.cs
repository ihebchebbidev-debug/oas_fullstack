using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Shared.Services;

namespace MyApi.Modules.Shared.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UploadController : ControllerBase
    {
        private readonly IUploadThingService _uploadThingService;
        private readonly ILogger<UploadController> _logger;

        public UploadController(IUploadThingService uploadThingService, ILogger<UploadController> logger)
        {
            _uploadThingService = uploadThingService;
            _logger = logger;
        }

        /// <summary>
        /// Upload a single file to UploadThing
        /// </summary>
        [HttpPost("file")]
        public async Task<ActionResult<UploadResponseDto>> UploadFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file provided");
                }

                // Validate file size (max 16MB)
                if (file.Length > 16 * 1024 * 1024)
                {
                    return BadRequest("File size exceeds 16MB limit");
                }

                _logger.LogInformation("Uploading file: {FileName}, Size: {Size}", file.FileName, file.Length);

                var result = await _uploadThingService.UploadFileAsync(file);

                if (!result.Success)
                {
                    _logger.LogError("Upload failed: {Error}", result.Error);
                    return StatusCode(500, new { error = result.Error });
                }

                return Ok(new UploadResponseDto
                {
                    Success = true,
                    FileUrl = result.FileUrl!,
                    FileKey = result.FileKey!,
                    FileName = result.FileName!,
                    FileSize = result.FileSize,
                    ContentType = result.ContentType!
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new { error = "An error occurred while uploading the file" });
            }
        }

        /// <summary>
        /// Upload multiple files to UploadThing
        /// </summary>
        [HttpPost("files")]
        public async Task<ActionResult<List<UploadResponseDto>>> UploadFiles([FromForm] List<IFormFile> files)
        {
            try
            {
                if (files == null || files.Count == 0)
                {
                    return BadRequest("No files provided");
                }

                // Validate total file count
                if (files.Count > 10)
                {
                    return BadRequest("Maximum 10 files allowed per upload");
                }

                // Validate each file size
                foreach (var file in files)
                {
                    if (file.Length > 16 * 1024 * 1024)
                    {
                        return BadRequest($"File {file.FileName} exceeds 16MB limit");
                    }
                }

                _logger.LogInformation("Uploading {Count} files", files.Count);

                var results = await _uploadThingService.UploadFilesAsync(files);
                var responseList = results.Select(r => new UploadResponseDto
                {
                    Success = r.Success,
                    FileUrl = r.FileUrl ?? "",
                    FileKey = r.FileKey ?? "",
                    FileName = r.FileName ?? "",
                    FileSize = r.FileSize,
                    ContentType = r.ContentType ?? "",
                    Error = r.Error
                }).ToList();

                return Ok(responseList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading files");
                return StatusCode(500, new { error = "An error occurred while uploading files" });
            }
        }

        /// <summary>
        /// Delete a file from UploadThing
        /// </summary>
        [HttpDelete("{fileKey}")]
        public async Task<ActionResult> DeleteFile(string fileKey)
        {
            try
            {
                if (string.IsNullOrEmpty(fileKey))
                {
                    return BadRequest("File key is required");
                }

                var success = await _uploadThingService.DeleteFileAsync(fileKey);

                if (!success)
                {
                    return StatusCode(500, new { error = "Failed to delete file" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file");
                return StatusCode(500, new { error = "An error occurred while deleting the file" });
            }
        }
    }

    public class UploadResponseDto
    {
        public bool Success { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string FileKey { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string? Error { get; set; }
    }
}
