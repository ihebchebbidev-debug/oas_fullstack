using MyApi.Data;
using MyApi.Modules.WebsiteBuilder.DTOs;
using MyApi.Modules.WebsiteBuilder.Models;
using Microsoft.EntityFrameworkCore;

namespace MyApi.Modules.WebsiteBuilder.Services
{
    // ══════════════════════════════════════════════════════════════
    // Global Block Service
    // ══════════════════════════════════════════════════════════════

    public class WBGlobalBlockService : IWBGlobalBlockService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WBGlobalBlockService> _logger;

        public WBGlobalBlockService(ApplicationDbContext context, ILogger<WBGlobalBlockService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<WBGlobalBlockResponseDto>> GetAllGlobalBlocksAsync()
        {
            var blocks = await _context.WBGlobalBlocks
                .AsNoTracking()
                .Include(b => b.Usages)
                .Where(b => !b.IsDeleted)
                .OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt)
                .ToListAsync();

            return blocks.Select(b => new WBGlobalBlockResponseDto
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                ComponentJson = b.ComponentJson,
                Category = b.Category,
                Tags = b.Tags,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
                CreatedBy = b.CreatedBy,
                UsageCount = b.Usages.Count
            }).ToList();
        }

        public async Task<WBGlobalBlockResponseDto?> GetGlobalBlockByIdAsync(int id)
        {
            var block = await _context.WBGlobalBlocks
                .AsNoTracking()
                .Include(b => b.Usages)
                .Where(b => b.Id == id && !b.IsDeleted)
                .FirstOrDefaultAsync();

            if (block == null) return null;

            return new WBGlobalBlockResponseDto
            {
                Id = block.Id,
                Name = block.Name,
                Description = block.Description,
                ComponentJson = block.ComponentJson,
                Category = block.Category,
                Tags = block.Tags,
                CreatedAt = block.CreatedAt,
                UpdatedAt = block.UpdatedAt,
                CreatedBy = block.CreatedBy,
                UsageCount = block.Usages.Count
            };
        }

        public async Task<WBGlobalBlockResponseDto> CreateGlobalBlockAsync(CreateWBGlobalBlockRequestDto createDto, string createdByUser)
        {
            var block = new WBGlobalBlock
            {
                Name = createDto.Name,
                Description = createDto.Description,
                ComponentJson = createDto.ComponentJson,
                Category = createDto.Category,
                Tags = createDto.Tags,
                CreatedBy = createdByUser,
                CreatedAt = DateTime.UtcNow
            };

            _context.WBGlobalBlocks.Add(block);
            await _context.SaveChangesAsync();

            _logger.LogInformation("WB Global Block created with ID {BlockId}", block.Id);
            return (await GetGlobalBlockByIdAsync(block.Id))!;
        }

        public async Task<WBGlobalBlockResponseDto?> UpdateGlobalBlockAsync(int id, UpdateWBGlobalBlockRequestDto updateDto, string modifiedByUser)
        {
            var block = await _context.WBGlobalBlocks
                .Where(b => b.Id == id && !b.IsDeleted)
                .FirstOrDefaultAsync();

            if (block == null) return null;

            if (!string.IsNullOrEmpty(updateDto.Name)) block.Name = updateDto.Name;
            if (updateDto.Description != null) block.Description = updateDto.Description;
            if (updateDto.ComponentJson != null) block.ComponentJson = updateDto.ComponentJson;
            if (updateDto.Category != null) block.Category = updateDto.Category;
            if (updateDto.Tags != null) block.Tags = updateDto.Tags;

            block.ModifiedBy = modifiedByUser;
            block.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetGlobalBlockByIdAsync(id);
        }

        public async Task<bool> DeleteGlobalBlockAsync(int id, string deletedByUser)
        {
            var block = await _context.WBGlobalBlocks
                .Where(b => b.Id == id && !b.IsDeleted)
                .FirstOrDefaultAsync();

            if (block == null) return false;

            block.IsDeleted = true;
            block.DeletedAt = DateTime.UtcNow;
            block.DeletedBy = deletedByUser;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> TrackUsageAsync(int globalBlockId, int siteId, int? pageId)
        {
            var existing = await _context.WBGlobalBlockUsages
                .AnyAsync(u => u.GlobalBlockId == globalBlockId && u.SiteId == siteId && u.PageId == pageId);

            if (existing) return true;

            _context.WBGlobalBlockUsages.Add(new WBGlobalBlockUsage
            {
                GlobalBlockId = globalBlockId,
                SiteId = siteId,
                PageId = pageId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return true;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Brand Profile Service
    // ══════════════════════════════════════════════════════════════

    public class WBBrandProfileService : IWBBrandProfileService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WBBrandProfileService> _logger;

        public WBBrandProfileService(ApplicationDbContext context, ILogger<WBBrandProfileService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<WBBrandProfileResponseDto>> GetAllBrandProfilesAsync()
        {
            var profiles = await _context.WBBrandProfiles
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.IsBuiltIn ? 0 : 1)
                .ThenBy(p => p.Name)
                .ToListAsync();

            return profiles.Select(p => new WBBrandProfileResponseDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                ThemeJson = p.ThemeJson,
                IsBuiltIn = p.IsBuiltIn,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                CreatedBy = p.CreatedBy,
            }).ToList();
        }

        public async Task<WBBrandProfileResponseDto?> GetBrandProfileByIdAsync(int id)
        {
            var profile = await _context.WBBrandProfiles
                .AsNoTracking()
                .Where(p => p.Id == id && !p.IsDeleted)
                .FirstOrDefaultAsync();

            if (profile == null) return null;

            return new WBBrandProfileResponseDto
            {
                Id = profile.Id,
                Name = profile.Name,
                Description = profile.Description,
                ThemeJson = profile.ThemeJson,
                IsBuiltIn = profile.IsBuiltIn,
                CreatedAt = profile.CreatedAt,
                UpdatedAt = profile.UpdatedAt,
                CreatedBy = profile.CreatedBy,
            };
        }

        public async Task<WBBrandProfileResponseDto> CreateBrandProfileAsync(CreateWBBrandProfileRequestDto createDto, string createdByUser)
        {
            var profile = new WBBrandProfile
            {
                Name = createDto.Name,
                Description = createDto.Description,
                ThemeJson = createDto.ThemeJson,
                IsBuiltIn = false,
                CreatedBy = createdByUser,
                CreatedAt = DateTime.UtcNow
            };

            _context.WBBrandProfiles.Add(profile);
            await _context.SaveChangesAsync();

            return (await GetBrandProfileByIdAsync(profile.Id))!;
        }

        public async Task<WBBrandProfileResponseDto?> UpdateBrandProfileAsync(int id, UpdateWBBrandProfileRequestDto updateDto, string modifiedByUser)
        {
            var profile = await _context.WBBrandProfiles
                .Where(p => p.Id == id && !p.IsDeleted && !p.IsBuiltIn)
                .FirstOrDefaultAsync();

            if (profile == null) return null;

            if (!string.IsNullOrEmpty(updateDto.Name)) profile.Name = updateDto.Name;
            if (updateDto.Description != null) profile.Description = updateDto.Description;
            if (updateDto.ThemeJson != null) profile.ThemeJson = updateDto.ThemeJson;

            profile.ModifiedBy = modifiedByUser;
            profile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetBrandProfileByIdAsync(id);
        }

        public async Task<bool> DeleteBrandProfileAsync(int id, string deletedByUser)
        {
            var profile = await _context.WBBrandProfiles
                .Where(p => p.Id == id && !p.IsDeleted && !p.IsBuiltIn)
                .FirstOrDefaultAsync();

            if (profile == null) return false;

            profile.IsDeleted = true;
            profile.DeletedAt = DateTime.UtcNow;
            profile.DeletedBy = deletedByUser;

            await _context.SaveChangesAsync();
            return true;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Form Submission Service
    // ══════════════════════════════════════════════════════════════

    public class WBFormSubmissionService : IWBFormSubmissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WBFormSubmissionService> _logger;

        public WBFormSubmissionService(ApplicationDbContext context, ILogger<WBFormSubmissionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<WBFormSubmissionListResponseDto> GetSubmissionsBySiteIdAsync(int siteId, int pageNumber = 1, int pageSize = 50)
        {
            var query = _context.WBFormSubmissions
                .AsNoTracking()
                // Wave 2: filter out soft-deleted submissions
                .Where(s => s.SiteId == siteId && !s.IsDeleted)
                .OrderByDescending(s => s.SubmittedAt);

            var totalCount = await query.CountAsync();

            var submissions = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new WBFormSubmissionListResponseDto
            {
                Submissions = submissions.Select(s => new WBFormSubmissionResponseDto
                {
                    Id = s.Id,
                    SiteId = s.SiteId,
                    PageId = s.PageId,
                    FormComponentId = s.FormComponentId,
                    FormLabel = s.FormLabel,
                    PageTitle = s.PageTitle,
                    DataJson = s.DataJson,
                    Source = s.Source,
                    WebhookStatus = s.WebhookStatus,
                    SubmittedAt = s.SubmittedAt,
                }).ToList(),
                TotalCount = totalCount
            };
        }

        public async Task<WBFormSubmissionResponseDto> CreateSubmissionAsync(CreateWBFormSubmissionRequestDto createDto, string? ipAddress)
        {
            var submission = new WBFormSubmission
            {
                SiteId = createDto.SiteId,
                PageId = createDto.PageId,
                FormComponentId = createDto.FormComponentId,
                FormLabel = createDto.FormLabel,
                PageTitle = createDto.PageTitle,
                DataJson = createDto.DataJson,
                Source = createDto.Source ?? "website",
                IpAddress = ipAddress,
                SubmittedAt = DateTime.UtcNow
            };

            _context.WBFormSubmissions.Add(submission);
            await _context.SaveChangesAsync();

            return new WBFormSubmissionResponseDto
            {
                Id = submission.Id,
                SiteId = submission.SiteId,
                PageId = submission.PageId,
                FormComponentId = submission.FormComponentId,
                FormLabel = submission.FormLabel,
                PageTitle = submission.PageTitle,
                DataJson = submission.DataJson,
                Source = submission.Source,
                SubmittedAt = submission.SubmittedAt,
            };
        }

        // Wave 2: soft-delete (GDPR audit trail).
        public async Task<bool> DeleteSubmissionAsync(int id)
        {
            var submission = await _context.WBFormSubmissions
                .Where(s => s.Id == id && !s.IsDeleted)
                .FirstOrDefaultAsync();
            if (submission == null) return false;

            submission.IsDeleted = true;
            submission.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ClearSubmissionsAsync(int siteId, string? formComponentId = null)
        {
            var query = _context.WBFormSubmissions
                .Where(s => s.SiteId == siteId && !s.IsDeleted);
            if (!string.IsNullOrEmpty(formComponentId))
            {
                query = query.Where(s => s.FormComponentId == formComponentId);
            }

            var submissions = await query.ToListAsync();
            var now = DateTime.UtcNow;
            foreach (var s in submissions)
            {
                s.IsDeleted = true;
                s.DeletedAt = now;
            }
            await _context.SaveChangesAsync();
            return true;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Media Service
    // ══════════════════════════════════════════════════════════════

    public class WBMediaService : IWBMediaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WBMediaService> _logger;

        public WBMediaService(ApplicationDbContext context, ILogger<WBMediaService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<WBMediaResponseDto>> GetMediaAsync(int? siteId = null, string? folder = null)
        {
            var query = _context.WBMedia
                .AsNoTracking()
                .Where(m => !m.IsDeleted);

            if (siteId.HasValue)
                query = query.Where(m => m.SiteId == siteId.Value || m.SiteId == null);

            if (!string.IsNullOrEmpty(folder))
                query = query.Where(m => m.Folder == folder);

            var media = await query
                .OrderByDescending(m => m.UploadedAt)
                .ToListAsync();

            return media.Select(MapToDto).ToList();
        }

        public async Task<WBMediaResponseDto?> GetMediaByIdAsync(int id)
        {
            var media = await _context.WBMedia
                .AsNoTracking()
                .Where(m => m.Id == id && !m.IsDeleted)
                .FirstOrDefaultAsync();

            return media != null ? MapToDto(media) : null;
        }

        /// <summary>
        /// Internal DTO used by GetMediaByIdAsync to include FilePath for disk operations.
        /// </summary>
        public async Task<WBMediaInternalDto?> GetMediaByIdInternalAsync(int id)
        {
            var media = await _context.WBMedia
                .AsNoTracking()
                .Where(m => m.Id == id && !m.IsDeleted)
                .FirstOrDefaultAsync();

            if (media == null) return null;

            return new WBMediaInternalDto
            {
                Id = media.Id,
                FileName = media.FileName,
                OriginalName = media.OriginalName,
                FilePath = media.FilePath,
                FileUrl = media.FileUrl,
                ContentType = media.ContentType,
            };
        }

        /// <summary>
        /// Public render path for &lt;img src&gt; references on published sites.
        /// Bypasses the tenant filter (no auth context) but only serves media
        /// whose owning site is currently Published. Site-less media
        /// (SiteId == null, e.g. tenant logos uploaded outside a site) is
        /// NOT exposed via this path to avoid cross-tenant ID-guessing leaks.
        /// </summary>
        public async Task<WBMediaInternalDto?> GetPublicMediaByIdAsync(int id)
        {
            var record = await _context.WBMedia
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.Id == id && !m.IsDeleted)
                .Select(m => new
                {
                    Media = m,
                    SitePublished = m.SiteId != null && _context.WBSites
                        .IgnoreQueryFilters()
                        .Any(s => s.Id == m.SiteId && !s.IsDeleted && s.Published)
                })
                .FirstOrDefaultAsync();

            if (record == null || !record.SitePublished) return null;

            var media = record.Media;
            return new WBMediaInternalDto
            {
                Id = media.Id,
                FileName = media.FileName,
                OriginalName = media.OriginalName,
                FilePath = media.FilePath,
                FileUrl = media.FileUrl,
                ContentType = media.ContentType,
            };
        }

        public async Task<WBMediaResponseDto> CreateMediaAsync(CreateWBMediaRequestDto createDto, string uploadedByUser)
        {
            var media = new WBMedia
            {
                SiteId = createDto.SiteId,
                FileName = createDto.FileName,
                OriginalName = createDto.OriginalName,
                FilePath = createDto.FilePath,
                FileUrl = createDto.FileUrl,
                FileSize = createDto.FileSize,
                ContentType = createDto.ContentType,
                Width = createDto.Width,
                Height = createDto.Height,
                Folder = createDto.Folder,
                AltText = createDto.AltText,
                UploadedBy = uploadedByUser,
                UploadedAt = DateTime.UtcNow
            };

            _context.WBMedia.Add(media);
            await _context.SaveChangesAsync();

            _logger.LogInformation("WB Media record created: Id={Id}, FilePath={FilePath}",
                media.Id, media.FilePath);

            return MapToDto(media);
        }

        public async Task<bool> UpdateFileUrlAsync(int id, string fileUrl)
        {
            var media = await _context.WBMedia.FindAsync(id);
            if (media == null) return false;

            media.FileUrl = fileUrl;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteMediaAsync(int id)
        {
            var media = await _context.WBMedia.FindAsync(id);
            if (media == null || media.IsDeleted) return false;

            media.IsDeleted = true;
            media.DeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("WB Media {Id} soft-deleted from database", id);
            return true;
        }

        private static WBMediaResponseDto MapToDto(WBMedia media)
        {
            return new WBMediaResponseDto
            {
                Id = media.Id,
                SiteId = media.SiteId,
                FileName = media.FileName,
                OriginalName = media.OriginalName,
                FileUrl = media.FileUrl,
                FileSize = media.FileSize,
                ContentType = media.ContentType,
                Width = media.Width,
                Height = media.Height,
                Folder = media.Folder,
                AltText = media.AltText,
                UploadedAt = media.UploadedAt,
                UploadedBy = media.UploadedBy,
            };
        }
    }

    /// <summary>
    /// Internal DTO that includes FilePath for disk operations (not exposed to frontend).
    /// </summary>
    public class WBMediaInternalDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════
    // Template Service
    // ══════════════════════════════════════════════════════════════

    public class WBTemplateService : IWBTemplateService
    {
        private readonly ApplicationDbContext _context;

        public WBTemplateService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<WBTemplateResponseDto>> GetAllTemplatesAsync()
        {
            var templates = await _context.WBTemplates
                .AsNoTracking()
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Name)
                .ToListAsync();

            return templates.Select(t => new WBTemplateResponseDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Category = t.Category,
                PreviewImageUrl = t.PreviewImageUrl,
                ThemeJson = t.ThemeJson,
                PagesJson = t.PagesJson,
                Tags = t.Tags,
                IsPremium = t.IsPremium,
                IsBuiltIn = t.IsBuiltIn,
                SortOrder = t.SortOrder,
            }).ToList();
        }

        public async Task<WBTemplateResponseDto?> GetTemplateByIdAsync(int id)
        {
            var template = await _context.WBTemplates
                .AsNoTracking()
                .Where(t => t.Id == id && !t.IsDeleted)
                .FirstOrDefaultAsync();

            if (template == null) return null;

            return new WBTemplateResponseDto
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                Category = template.Category,
                PreviewImageUrl = template.PreviewImageUrl,
                ThemeJson = template.ThemeJson,
                PagesJson = template.PagesJson,
                Tags = template.Tags,
                IsPremium = template.IsPremium,
                IsBuiltIn = template.IsBuiltIn,
                SortOrder = template.SortOrder,
            };
        }

        public async Task<List<string>> GetTemplateCategoriesAsync()
        {
            return await _context.WBTemplates
                .AsNoTracking()
                .Where(t => !t.IsDeleted)
                .Select(t => t.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Activity Log Service
    // ══════════════════════════════════════════════════════════════

    public class WBActivityLogService : IWBActivityLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WBActivityLogService> _logger;

        public WBActivityLogService(ApplicationDbContext context, ILogger<WBActivityLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogActivityAsync(int siteId, int? pageId, string action, string entityType, string? details, string createdByUser)
        {
            try
            {
                _context.WBActivityLogs.Add(new WBActivityLog
                {
                    SiteId = siteId,
                    PageId = pageId,
                    Action = action,
                    EntityType = entityType,
                    Details = details,
                    CreatedBy = createdByUser,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Don't let logging failures break the main operation
                _logger.LogWarning(ex, "Failed to log WB activity: {Action} on {EntityType}", action, entityType);
            }
        }

        public async Task<List<WBActivityLogResponseDto>> GetActivityLogAsync(int siteId, int count = 50)
        {
            var logs = await _context.WBActivityLogs
                .AsNoTracking()
                .Where(l => l.SiteId == siteId)
                .OrderByDescending(l => l.CreatedAt)
                .Take(count)
                .ToListAsync();

            return logs.Select(l => new WBActivityLogResponseDto
            {
                Id = l.Id,
                SiteId = l.SiteId,
                PageId = l.PageId,
                Action = l.Action,
                EntityType = l.EntityType,
                Details = l.Details,
                CreatedAt = l.CreatedAt,
                CreatedBy = l.CreatedBy,
            }).ToList();
        }
    }
}
