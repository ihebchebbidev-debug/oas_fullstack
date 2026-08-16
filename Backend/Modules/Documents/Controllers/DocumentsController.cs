using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Documents.Models;
using System.IO.Compression;

namespace MyApi.Modules.Documents.Controllers
{
    /// <summary>
    /// Service for document compression using GZip
    /// Reduces file size on disk without affecting quality
    /// </summary>
    public class DocumentCompressionService
    {
        private readonly ILogger<DocumentCompressionService> _logger;
        private const int CompressionBufferSize = 81920; // 80 KB buffer

        // File types to compress (text-based, documents, etc.)
        private static readonly HashSet<string> CompressibleExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Office documents
            ".pdf", ".docx", ".xlsx", ".pptx", ".odt", ".ods", ".odp",
            // Text files
            ".txt", ".csv", ".json", ".xml", ".html", ".css", ".js", ".ts",
            // Source code
            ".cs", ".java", ".py", ".php", ".rb", ".go", ".rs", ".cpp", ".c",
            // Archives
            ".sql", ".sql", ".log",
            // Documents
            ".md", ".yml", ".yaml", ".ini", ".conf", ".config"
        };

        public DocumentCompressionService(ILogger<DocumentCompressionService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Check if a file should be compressed based on extension
        /// </summary>
        public bool ShouldCompress(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            return CompressibleExtensions.Contains(ext);
        }

        /// <summary>
        /// Compress a file using GZip
        /// Returns: (compressedPath, originalSize, compressionRatio)
        /// </summary>
        public async Task<(string compressedPath, long originalSize, decimal compressionRatio)> CompressFileAsync(
            string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException($"Source file not found: {sourceFilePath}");

            var fileInfo = new FileInfo(sourceFilePath);
            var originalSize = fileInfo.Length;

            // Add .gz extension to indicate compressed file
            var compressedPath = sourceFilePath + ".gz";

            try
            {
                // Compress file using GZip with buffering
                using (var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, CompressionBufferSize, true))
                using (var targetStream = new FileStream(compressedPath, FileMode.Create, FileAccess.Write, FileShare.None, CompressionBufferSize, true))
                using (var gzipStream = new GZipStream(targetStream, CompressionMode.Compress, leaveOpen: true))
                {
                    await sourceStream.CopyToAsync(gzipStream, CompressionBufferSize);
                }

                // Calculate compression ratio
                var compressedFileInfo = new FileInfo(compressedPath);
                var compressedSize = compressedFileInfo.Length;
                var compressionRatio = ((decimal)(originalSize - compressedSize) / originalSize) * 100;

                _logger.LogInformation(
                    "Compressed file: {FileName} | Original: {Original}KB | Compressed: {Compressed}KB | Ratio: {Ratio}%",
                    Path.GetFileName(sourceFilePath),
                    originalSize / 1024,
                    compressedSize / 1024,
                    Math.Round(compressionRatio, 2)
                );

                // Delete original file after successful compression
                File.Delete(sourceFilePath);

                return (compressedPath, originalSize, compressionRatio);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compressing file: {FilePath}", sourceFilePath);
                // Delete compressed file if compression failed
                if (File.Exists(compressedPath))
                    File.Delete(compressedPath);
                throw;
            }
        }

        /// <summary>
        /// Decompress a file on-the-fly for download
        /// Returns decompressed stream
        /// </summary>
        public Stream DecompressFileForDownload(string compressedFilePath)
        {
            if (!File.Exists(compressedFilePath))
                throw new FileNotFoundException($"Compressed file not found: {compressedFilePath}");

            try
            {
                // Open the compressed file and return a GZipStream wrapper
                var fileStream = new FileStream(compressedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, CompressionBufferSize, true);
                var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress, leaveOpen: true);

                _logger.LogInformation("Decompressing file for download: {FilePath}", Path.GetFileName(compressedFilePath));

                return gzipStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decompressing file: {FilePath}", compressedFilePath);
                throw;
            }
        }

        /// <summary>
        /// Get decompressed file size without decompressing entire file
        /// </summary>
        public long GetDecompressedSize(string compressedFilePath)
        {
            try
            {
                using (var fileStream = new FileStream(compressedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    // GZip stores original size in the last 4 bytes (for files < 4GB)
                    fileStream.Seek(-4, SeekOrigin.End);
                    byte[] sizeBytes = new byte[4];
                    fileStream.Read(sizeBytes, 0, 4);
                    
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(sizeBytes);

                    return BitConverter.ToInt32(sizeBytes, 0);
                }
            }
            catch
            {
                // Fallback: return file size if we can't read from gzip header
                return new FileInfo(compressedFilePath).Length;
            }
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DocumentsController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly DocumentCompressionService _compressionService;
        private readonly MyApi.Modules.Shared.Services.IActivityLogger? _activityLogger;

        public DocumentsController(
            ApplicationDbContext db,
            ILogger<DocumentsController> logger,
            IWebHostEnvironment env,
            ILogger<DocumentCompressionService> compressionLogger,
            MyApi.Modules.Shared.Services.IActivityLogger? activityLogger = null)
        {
            _db = db;
            _logger = logger;
            _env = env;
            _compressionService = new DocumentCompressionService(compressionLogger);
            _activityLogger = activityLogger;
        }

        // Map "sales|offers|deals|projects|services|field" module tags to the
        // parent entity type understood by IActivityLogger. Anything else (e.g.
        // "contacts", "general") is treated as unparented — SystemLog only.
        private static string? ResolveParentEntityType(string? moduleType) => moduleType?.ToLowerInvariant() switch
        {
            "offers" or "offer" => "Offer",
            "sales" or "sale" => "Sale",
            "deals" or "deal" => "Deal",
            "projects" or "project" => "Project",
            // ServiceOrders + Dispatches have no per-entity Activity table; log
            // as SystemLog only (still auditable, just not on a parent timeline).
            _ => null,
        };

        private async Task LogDocumentActivityAsync(string action, Document doc, string message)
        {
            if (_activityLogger == null) return;
            var parentType = ResolveParentEntityType(doc.ModuleType);
            int? parentId = null;
            if (parentType != null && int.TryParse(doc.ModuleId, out var pid)) parentId = pid;

            var userId = User?.FindFirst("sub")?.Value
                ?? User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User?.Identity?.Name
                ?? User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            await _activityLogger.LogAsync(new MyApi.Modules.Shared.Services.ActivityLogEntry
            {
                Module = "Documents",
                Action = action,
                EntityType = "Document",
                EntityId = doc.Id.ToString(),
                ParentEntityType = parentType,
                ParentEntityId = parentId,
                UserId = userId,
                UserName = userName,
                Message = message,
                Details = doc.FileName,
            });
        }

        /// <summary>
        /// Get the uploads root folder (one level above the backend folder)
        /// </summary>
        private string GetUploadsRoot()
        {
            var backendRoot = _env.ContentRootPath;
            var parentDir = Directory.GetParent(backendRoot)?.FullName ?? backendRoot;
            var uploadsDir = Path.Combine(parentDir, "uploads");

            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
                _logger.LogInformation("Created uploads directory at: {Path}", uploadsDir);
            }

            return uploadsDir;
        }

        /// <summary>
        /// Resolve a DB FilePath to an absolute disk path.
        /// FilePath is stored as "/uploads/subFolder/file.ext".
        /// </summary>
        private string ResolveFilePath(string dbFilePath)
        {
            var relative = dbFilePath.TrimStart('/');
            if (relative.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                relative = relative.Substring("uploads/".Length);

            return Path.Combine(GetUploadsRoot(), relative);
        }

        /// <summary>
        /// GET /api/Documents — List documents with optional filters
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> GetDocuments(
            [FromQuery] string? search,
            [FromQuery] string? moduleType,
            [FromQuery] string? fileType,
            [FromQuery] string? category,
            [FromQuery] string? uploadedBy,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var query = _db.Documents.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    var s = search.ToLower();
                    query = query.Where(d =>
                        d.FileName.ToLower().Contains(s) ||
                        d.OriginalName.ToLower().Contains(s) ||
                        (d.Description != null && d.Description.ToLower().Contains(s)) ||
                        (d.ModuleName != null && d.ModuleName.ToLower().Contains(s))
                    );
                }

                if (!string.IsNullOrEmpty(moduleType))
                    query = query.Where(d => d.ModuleType == moduleType);

                if (!string.IsNullOrEmpty(fileType))
                {
                    var ext = fileType.ToLower();
                    query = query.Where(d => d.FileName.ToLower().EndsWith("." + ext));
                }

                if (!string.IsNullOrEmpty(category))
                    query = query.Where(d => d.Category == category);

                if (!string.IsNullOrEmpty(uploadedBy))
                    query = query.Where(d => d.UploadedBy == uploadedBy);

                var total = await query.CountAsync();

                var documents = await query
                    .OrderByDescending(d => d.UploadedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Ok(new { data = documents, total, page, pageSize });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching documents");
                return StatusCode(500, new { error = "Failed to fetch documents" });
            }
        }

        /// <summary>
        /// GET /api/Documents/stats — Get document statistics including compression metrics
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult> GetStats()
        {
            try
            {
                var totalFiles = await _db.Documents.CountAsync();
                var totalSize = await _db.Documents.SumAsync(d => d.FileSize);
                var totalOriginalSize = await _db.Documents.SumAsync(d => d.OriginalFileSize ?? d.FileSize);
                var compressedFiles = await _db.Documents.CountAsync(d => d.IsCompressed);
                var totalCompressionSaved = totalOriginalSize - totalSize;
                var overallCompressionRatio = totalOriginalSize > 0 ? ((decimal)totalCompressionSaved / totalOriginalSize) * 100 : 0;

                var crmFiles = await _db.Documents.CountAsync(d => d.Category == "crm");
                var fieldFiles = await _db.Documents.CountAsync(d => d.Category == "field");

                var byModule = new
                {
                    contacts = await _db.Documents.CountAsync(d => d.ModuleType == "contacts"),
                    sales = await _db.Documents.CountAsync(d => d.ModuleType == "sales"),
                    offers = await _db.Documents.CountAsync(d => d.ModuleType == "offers"),
                    services = await _db.Documents.CountAsync(d => d.ModuleType == "services"),
                    projects = await _db.Documents.CountAsync(d => d.ModuleType == "projects"),
                    field = await _db.Documents.CountAsync(d => d.ModuleType == "field"),
                    general = await _db.Documents.CountAsync(d => d.ModuleType == "general" || d.ModuleType == null),
                };

                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                var recentActivity = await _db.Documents.CountAsync(d => d.UploadedAt >= sevenDaysAgo);

                return Ok(new 
                { 
                    totalFiles, 
                    totalSize, 
                    crmFiles, 
                    fieldFiles, 
                    byModule, 
                    recentActivity,
                    // ✅ NEW: Compression statistics
                    compression = new
                    {
                        compressedFiles,
                        uncompressedFiles = totalFiles - compressedFiles,
                        totalOriginalSize,
                        totalStorageSaved = totalCompressionSaved,
                        overallCompressionRatio = Math.Round(overallCompressionRatio, 2),
                        storageSavedMB = Math.Round((decimal)totalCompressionSaved / (1024 * 1024), 2)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching document stats");
                return StatusCode(500, new { error = "Failed to fetch stats" });
            }
        }

        /// <summary>
        /// GET /api/Documents/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult> GetDocument(int id)
        {
            var doc = await _db.Documents.FindAsync(id);
            if (doc == null)
                return NotFound(new { error = "Document not found" });

            return Ok(doc);
        }

        /// <summary>
        /// POST /api/Documents — Create a document record without uploading a file (e.g., external link)
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> CreateDocument([FromBody] CreateDocumentRequest request)
        {
            try
            {
                // Extract user info from JWT
                var userId = User.FindFirst("sub")?.Value
                    ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                    ?? "unknown";
                var userName = User.FindFirst("name")?.Value
                    ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value
                    ?? User.Identity?.Name
                    ?? "Unknown User";

                if (request == null)
                    return BadRequest(new { error = "Invalid payload" });

                // Determine file name for the link (use title or fallback to url)
                var safeFileName = string.IsNullOrEmpty(request.Title)
                    ? (request.ExternalUrl != null ? Path.GetFileName(new Uri(request.ExternalUrl).AbsolutePath) : "link")
                    : SanitizeFileName(request.Title.Replace(' ', '_'));

                var doc = new Models.Document
                {
                    FileName = safeFileName,
                    OriginalName = request.Title ?? safeFileName,
                    // For link-only documents store URL in ExternalUrl and mark resource type
                    FilePath = string.Empty,
                    ExternalUrl = request.ExternalUrl ?? string.Empty,
                    FileSize = 0,
                    OriginalFileSize = 0,
                    ContentType = request.ContentType ?? "link",
                    ResourceType = string.IsNullOrEmpty(request.ExternalUrl) ? "file" : "link",
                    ModuleType = string.IsNullOrEmpty(request.ModuleType) ? "general" : request.ModuleType,
                    ModuleId = string.IsNullOrEmpty(request.ModuleId) ? null : request.ModuleId,
                    ModuleName = string.IsNullOrEmpty(request.ModuleName) ? null : request.ModuleName,
                    Category = string.IsNullOrEmpty(request.Category) ? "crm" : request.Category,
                    Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                    Tags = request.Tags == null ? null : string.Join(',', request.Tags),
                    IsPublic = request.IsPublic,
                    UploadedBy = userId,
                    UploadedByName = userName,
                    UploadedAt = DateTime.UtcNow,
                    IsCompressed = false,
                    CompressionMethod = "none",
                };

                _db.Documents.Add(doc);
                await _db.SaveChangesAsync();

                await LogDocumentActivityAsync("document_linked",
                    doc,
                    doc.ResourceType == "link"
                        ? $"Linked document: {doc.OriginalName} → {doc.ExternalUrl}"
                        : $"Document record created: {doc.OriginalName}");

                return Ok(new { success = true, document = doc });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating document");
                return StatusCode(500, new { error = "Failed to create document" });
            }
        }

        /// <summary>
        /// POST /api/Documents/upload — Upload file(s) and save metadata
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
        public async Task<ActionResult> UploadDocuments(
            [FromForm] List<IFormFile> files,
            [FromForm] string? moduleType,
            [FromForm] string? moduleId,
            [FromForm] string? moduleName,
            [FromForm] string? category,
            [FromForm] string? description,
            [FromForm] string? tags,
            [FromForm] bool isPublic = false)
        {
            try
            {
                if (files == null || files.Count == 0)
                    return BadRequest(new { error = "No files provided" });

                var uploadsRoot = GetUploadsRoot();
                var subFolder = !string.IsNullOrEmpty(moduleType) ? moduleType : "general";
                var targetDir = Path.Combine(uploadsRoot, subFolder);

                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                // Extract user info from JWT
                var userId = User.FindFirst("sub")?.Value
                    ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                    ?? "unknown";
                var userName = User.FindFirst("name")?.Value
                    ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value
                    ?? User.Identity?.Name
                    ?? "Unknown User";

                var uploadedDocs = new List<Document>();

                foreach (var file in files)
                {
                    if (file.Length == 0) continue;

                    // Unique file name: timestamp + guid suffix to prevent collisions
                    var safeFileName = SanitizeFileName(file.FileName);
                    var uniqueFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid().ToString("N")[..8]}_{safeFileName}";
                    var diskPath = Path.Combine(targetDir, uniqueFileName);

                    // Stream file to disk (memory-efficient for large files)
                    await using (var stream = new FileStream(diskPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var relativePath = $"/uploads/{subFolder}/{uniqueFileName}";
                    var originalFileSize = file.Length;
                    var isCompressed = false;
                    var compressionRatio = 0m;
                    var compressionMethod = "none";

                    // Compress file if it's a compressible type
                    try
                    {
                        if (_compressionService.ShouldCompress(safeFileName))
                        {
                            var (compressedPath, origSize, ratio) = await _compressionService.CompressFileAsync(diskPath);
                            
                            // Update to use the compressed file path
                            diskPath = compressedPath;
                            relativePath = $"/uploads/{subFolder}/{uniqueFileName}.gz";
                            isCompressed = true;
                            compressionRatio = ratio;
                            compressionMethod = "gzip";

                            _logger.LogInformation(
                                "Compressed document: {FileName} | Saved {SavingsPercent}% space",
                                safeFileName,
                                Math.Round(ratio, 2)
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to compress file {FileName}, storing uncompressed", safeFileName);
                    }

                    var doc = new Document
                    {
                        FileName = safeFileName,
                        OriginalName = file.FileName,
                        FilePath = relativePath,
                        ExternalUrl = null,
                        FileSize = new FileInfo(diskPath).Length, // Actual size on disk
                        OriginalFileSize = originalFileSize,
                        ContentType = file.ContentType ?? "application/octet-stream",
                        ModuleType = string.IsNullOrEmpty(moduleType) ? "general" : moduleType,
                        ModuleId = string.IsNullOrEmpty(moduleId) ? null : moduleId,
                        ModuleName = string.IsNullOrEmpty(moduleName) ? null : moduleName,
                        Category = string.IsNullOrEmpty(category) ? "crm" : category,
                        Description = string.IsNullOrEmpty(description) ? null : description,
                        Tags = string.IsNullOrEmpty(tags) ? null : tags,
                        IsPublic = isPublic,
                        IsCompressed = isCompressed,
                        CompressionRatio = compressionRatio,
                        CompressionMethod = compressionMethod,
                        ResourceType = "file",
                        UploadedBy = userId,
                        UploadedByName = userName,
                        UploadedAt = DateTime.UtcNow,
                    };

                    _db.Documents.Add(doc);
                    uploadedDocs.Add(doc);
                }

                await _db.SaveChangesAsync();

                foreach (var d in uploadedDocs)
                {
                    await LogDocumentActivityAsync("document_uploaded", d,
                        $"Document uploaded: {d.OriginalName} ({d.OriginalFileSize} bytes)");
                }

                _logger.LogInformation("Uploaded {Count} document(s) by user {User}", uploadedDocs.Count, userId);

                return Ok(new
                {
                    success = true,
                    message = $"{uploadedDocs.Count} file(s) uploaded successfully",
                    documents = uploadedDocs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading documents");
                return StatusCode(500, new { error = "Failed to upload documents" });
            }
        }

        /// <summary>
        /// GET /api/Documents/download/{id} — Stream file download (auto-decompresses if needed)
        /// </summary>
        [HttpGet("download/{id}")]
        public async Task<ActionResult> DownloadDocument(int id)
        {
            try
            {
                var doc = await _db.Documents.FindAsync(id);
                if (doc == null)
                    return NotFound(new { error = "Document not found" });

                var fullPath = ResolveFilePath(doc.FilePath);

                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning("File not found on disk: {Path} (DB Id={Id})", fullPath, id);
                    return NotFound(new { error = "File not found on server" });
                }

                // If file is compressed, decompress on-the-fly
                if (doc.IsCompressed)
                {
                    try
                    {
                        var decompressedStream = _compressionService.DecompressFileForDownload(fullPath);
                        _logger.LogInformation("Serving decompressed file: {FileName} (saved {Ratio}% storage)", doc.FileName, Math.Round(doc.CompressionRatio ?? 0, 2));
                        return File(decompressedStream, doc.ContentType ?? "application/octet-stream", doc.OriginalName ?? doc.FileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error decompressing file {Id}, falling back to compressed download", id);
                        // Fallback to compressed file if decompression fails
                        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                        return File(stream, doc.ContentType ?? "application/octet-stream", doc.OriginalName ?? doc.FileName);
                    }
                }

                // Stream uncompressed file
                var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                return File(fileStream, doc.ContentType ?? "application/octet-stream", doc.OriginalName ?? doc.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {Id}", id);
                return StatusCode(500, new { error = "Failed to download document" });
            }
        }

        /// <summary>
        /// DELETE /api/Documents/{id} — Delete a document and its file
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteDocument(int id)
        {
            try
            {
                var doc = await _db.Documents.FindAsync(id);
                if (doc == null)
                    return NotFound(new { error = "Document not found" });

                // Delete physical file
                var fullPath = ResolveFilePath(doc.FilePath);
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    _logger.LogInformation("Deleted file: {Path}", fullPath);
                }

                _db.Documents.Remove(doc);
                await _db.SaveChangesAsync();

                await LogDocumentActivityAsync("document_deleted", doc,
                    $"Document deleted: {doc.OriginalName}");

                return Ok(new { success = true, message = "Document deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {Id}", id);
                return StatusCode(500, new { error = "Failed to delete document" });
            }
        }

        /// <summary>
        /// POST /api/Documents/bulk-delete — Delete multiple documents at once
        /// </summary>
        [HttpPost("bulk-delete")]
        public async Task<ActionResult> BulkDeleteDocuments([FromBody] BulkDeleteRequest request)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
                return BadRequest(new { error = "No document IDs provided" });

            try
            {
                var ids = request.Ids.Select(id => int.TryParse(id, out var n) ? n : -1).Where(n => n > 0).ToList();
                var docs = await _db.Documents.Where(d => ids.Contains(d.Id)).ToListAsync();

                foreach (var doc in docs)
                {
                    var fullPath = ResolveFilePath(doc.FilePath);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }

                _db.Documents.RemoveRange(docs);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Bulk deleted {Count} document(s)", docs.Count);

                return Ok(new { success = true, message = $"{docs.Count} document(s) deleted", deletedCount = docs.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting documents");
                return StatusCode(500, new { error = "Failed to delete documents" });
            }
        }

        /// <summary>
        /// Sanitize file name — remove invalid path characters
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            // Collapse multiple underscores
            while (sanitized.Contains("__"))
                sanitized = sanitized.Replace("__", "_");
            return sanitized.Trim('_');
        }
    }

    public class BulkDeleteRequest
    {
        public List<string> Ids { get; set; } = new();
    }

    public class CreateDocumentRequest
    {
        public string? ModuleType { get; set; }
        public string? ModuleId { get; set; }
        public string? ModuleName { get; set; }
        public string? Title { get; set; }
        public string? ExternalUrl { get; set; }
        public string? Description { get; set; }
        public List<string>? Tags { get; set; }
        public string? Category { get; set; }
        public bool IsPublic { get; set; } = false;
        public string? ContentType { get; set; }
    }
}
