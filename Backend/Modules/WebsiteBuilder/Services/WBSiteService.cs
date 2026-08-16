using MyApi.Data;
using MyApi.Modules.WebsiteBuilder.DTOs;
using MyApi.Modules.WebsiteBuilder.Models;
using Microsoft.EntityFrameworkCore;

namespace MyApi.Modules.WebsiteBuilder.Services
{
    public class WBSiteService : IWBSiteService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWBActivityLogService _activityLog;
        private readonly ILogger<WBSiteService> _logger;

        public WBSiteService(ApplicationDbContext context, IWBActivityLogService activityLog, ILogger<WBSiteService> logger)
        {
            _context = context;
            _activityLog = activityLog;
            _logger = logger;
        }

        public async Task<WBSiteListResponseDto> GetAllSitesAsync(WBSiteSearchRequestDto? searchRequest = null)
        {
            try
            {
                var query = _context.WBSites
                    .AsNoTracking()
                    .Include(s => s.Pages.Where(p => !p.IsDeleted))
                    .Where(s => !s.IsDeleted);

                // Apply filters
                if (searchRequest != null)
                {
                    if (!string.IsNullOrEmpty(searchRequest.SearchTerm))
                    {
                        var term = searchRequest.SearchTerm.ToLower();
                        query = query.Where(s =>
                            s.Name.ToLower().Contains(term) ||
                            (s.Description != null && s.Description.ToLower().Contains(term)));
                    }

                    if (searchRequest.Published.HasValue)
                    {
                        query = query.Where(s => s.Published == searchRequest.Published.Value);
                    }

                    // Sorting
                    var isDesc = searchRequest.SortDirection?.ToLower() == "desc";
                    query = searchRequest.SortBy?.ToLower() switch
                    {
                        "name" => isDesc ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
                        "createdat" => isDesc ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt),
                        _ => isDesc ? query.OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt) : query.OrderBy(s => s.UpdatedAt ?? s.CreatedAt)
                    };
                }
                else
                {
                    query = query.OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt);
                }

                var totalCount = await query.CountAsync();

                var pageNumber = searchRequest?.PageNumber ?? 1;
                var pageSize = searchRequest?.PageSize ?? 20;
                var sites = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new WBSiteListResponseDto
                {
                    Sites = sites.Select(site => MapToSiteDto(site)).ToList(),
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all WB sites");
                throw;
            }
        }

        public async Task<WBSiteResponseDto?> GetSiteByIdAsync(int id)
        {
            try
            {
                var site = await _context.WBSites
                    .AsNoTracking()
                    .Include(s => s.Pages.Where(p => !p.IsDeleted).OrderBy(p => p.SortOrder))
                    .Where(s => s.Id == id && !s.IsDeleted)
                    .FirstOrDefaultAsync();

                return site != null ? MapToSiteDto(site) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting WB site {SiteId}", id);
                throw;
            }
        }

        public async Task<WBSiteResponseDto?> GetSiteBySlugAsync(string slug)
        {
            try
            {
                var site = await _context.WBSites
                    .AsNoTracking()
                    .Include(s => s.Pages.Where(p => !p.IsDeleted).OrderBy(p => p.SortOrder))
                    .Where(s => s.Slug == slug && !s.IsDeleted)
                    .FirstOrDefaultAsync();

                return site != null ? MapToSiteDto(site) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting WB site by slug {Slug}", slug);
                throw;
            }
        }

        /// <summary>
        /// Public render path. Bypasses the tenant query filter (anonymous
        /// requests carry no tenant header) but enforces Published==true so
        /// only opted-in sites are exposed. Pages are still soft-delete filtered.
        /// </summary>
        public async Task<WBSiteResponseDto?> GetPublishedSiteBySlugAsync(string slug)
        {
            try
            {
                var site = await _context.WBSites
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(s => s.Pages.Where(p => !p.IsDeleted).OrderBy(p => p.SortOrder))
                    .Where(s => s.Slug == slug && !s.IsDeleted && s.Published)
                    .FirstOrDefaultAsync();

                return site != null ? MapToSiteDto(site, publicSnapshot: true) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting published WB site by slug {Slug}", slug);
                throw;
            }
        }

        public async Task<WBSiteResponseDto> CreateSiteAsync(CreateWBSiteRequestDto createDto, string createdByUser)
        {
            try
            {
                var slug = GenerateSlug(createDto.Name);

                // Best-effort uniqueness check inside current tenant (the global
                // query filter scopes to TenantId automatically). The DB-level
                // partial unique index (TenantId, Slug) WHERE NOT IsDeleted is
                // the authoritative guard against races.
                var existingSlug = await _context.WBSites.AnyAsync(s => s.Slug == slug && !s.IsDeleted);
                if (existingSlug)
                {
                    slug = $"{slug}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                }

                var site = new WBSite
                {
                    Name = createDto.Name,
                    Slug = slug,
                    Description = createDto.Description,
                    ThemeJson = createDto.ThemeJson ?? "{}",
                    DefaultLanguage = createDto.DefaultLanguage ?? "en",
                    CreatedBy = createdByUser,
                    CreatedAt = DateTime.UtcNow
                };

                _context.WBSites.Add(site);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    throw new WBSlugConflictException($"A site with slug '{slug}' already exists.");
                }

                // Create pages
                if (createDto.Pages != null && createDto.Pages.Any())
                {
                    foreach (var pageDto in createDto.Pages)
                    {
                        var page = new WBPage
                        {
                            SiteId = site.Id,
                            Title = pageDto.Title,
                            Slug = pageDto.Slug ?? GenerateSlug(pageDto.Title),
                            ComponentsJson = pageDto.ComponentsJson ?? "[]",
                            SeoJson = pageDto.SeoJson ?? $"{{\"title\":\"{pageDto.Title}\"}}",
                            IsHomePage = pageDto.IsHomePage,
                            SortOrder = pageDto.SortOrder,
                            CreatedBy = createdByUser,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.WBPages.Add(page);
                    }
                }
                else
                {
                    _context.WBPages.Add(new WBPage
                    {
                        SiteId = site.Id,
                        Title = "Home",
                        Slug = "",
                        ComponentsJson = "[]",
                        SeoJson = $"{{\"title\":\"{createDto.Name}\"}}",
                        IsHomePage = true,
                        SortOrder = 0,
                        CreatedBy = createdByUser,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    throw new WBSlugConflictException("Two pages in this site share the same slug.");
                }

                await _activityLog.LogActivityAsync(site.Id, null, "create", "site", $"Site '{site.Name}' created", createdByUser);

                _logger.LogInformation("WB Site created with ID {SiteId}", site.Id);
                return (await GetSiteByIdAsync(site.Id))!;
            }
            catch (WBSlugConflictException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating WB site");
                throw;
            }
        }

        /// <summary>True when a DbUpdateException is a PostgreSQL 23505 unique-violation.</summary>
        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message ?? string.Empty;
            return msg.Contains("23505") || msg.Contains("duplicate key value violates unique constraint");
        }

        public async Task<WBSiteResponseDto?> UpdateSiteAsync(int id, UpdateWBSiteRequestDto updateDto, string modifiedByUser)
        {
            try
            {
                var site = await _context.WBSites
                    .Where(s => s.Id == id && !s.IsDeleted)
                    .FirstOrDefaultAsync();

                if (site == null) return null;

                if (!string.IsNullOrEmpty(updateDto.Name)) site.Name = updateDto.Name;
                if (!string.IsNullOrEmpty(updateDto.Slug)) site.Slug = updateDto.Slug;
                if (updateDto.Description != null) site.Description = updateDto.Description;
                if (updateDto.Favicon != null) site.Favicon = updateDto.Favicon;
                if (updateDto.ThemeJson != null) site.ThemeJson = updateDto.ThemeJson;
                if (updateDto.Published.HasValue) site.Published = updateDto.Published.Value;
                if (updateDto.DefaultLanguage != null) site.DefaultLanguage = updateDto.DefaultLanguage;
                if (updateDto.LanguagesJson != null) site.LanguagesJson = updateDto.LanguagesJson;

                site.ModifiedBy = modifiedByUser;
                site.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await _activityLog.LogActivityAsync(site.Id, null, "update", "site", $"Site '{site.Name}' updated", modifiedByUser);

                return await GetSiteByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating WB site {SiteId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteSiteAsync(int id, string deletedByUser)
        {
            try
            {
                var site = await _context.WBSites
                    .Where(s => s.Id == id && !s.IsDeleted)
                    .FirstOrDefaultAsync();

                if (site == null) return false;

                site.IsDeleted = true;
                site.DeletedAt = DateTime.UtcNow;
                site.DeletedBy = deletedByUser;

                await _context.SaveChangesAsync();

                await _activityLog.LogActivityAsync(site.Id, null, "delete", "site", $"Site '{site.Name}' deleted", deletedByUser);

                _logger.LogInformation("WB Site {SiteId} soft deleted", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting WB site {SiteId}", id);
                throw;
            }
        }

        public async Task<WBSiteResponseDto?> DuplicateSiteAsync(int id, string createdByUser)
        {
            try
            {
                var original = await _context.WBSites
                    .AsNoTracking()
                    .Include(s => s.Pages.Where(p => !p.IsDeleted))
                    .Where(s => s.Id == id && !s.IsDeleted)
                    .FirstOrDefaultAsync();

                if (original == null) return null;

                var createDto = new CreateWBSiteRequestDto
                {
                    Name = $"{original.Name} (copy)",
                    Description = original.Description,
                    ThemeJson = original.ThemeJson,
                    DefaultLanguage = original.DefaultLanguage,
                    Pages = original.Pages.Select(p => new CreateWBPageRequestDto
                    {
                        SiteId = 0, // Will be set in CreateSiteAsync
                        Title = p.Title,
                        Slug = p.Slug,
                        ComponentsJson = p.ComponentsJson,
                        SeoJson = p.SeoJson,
                        IsHomePage = p.IsHomePage,
                        SortOrder = p.SortOrder,
                    }).ToList()
                };

                var duplicated = await CreateSiteAsync(createDto, createdByUser);

                // Copy LanguagesJson at site level (not in CreateDto)
                if (!string.IsNullOrEmpty(original.LanguagesJson))
                {
                    var dupSite = await _context.WBSites.FindAsync(duplicated.Id);
                    if (dupSite != null)
                    {
                        dupSite.LanguagesJson = original.LanguagesJson;
                        await _context.SaveChangesAsync();
                    }
                }

                // Copy TranslationsJson per page (lost during create since DTO doesn't have it)
                if (duplicated.Pages.Any() && original.Pages.Any(p => !string.IsNullOrEmpty(p.TranslationsJson)))
                {
                    var originalPagesBySlug = original.Pages
                        .Where(p => !string.IsNullOrEmpty(p.TranslationsJson))
                        .ToDictionary(p => p.Slug, p => p.TranslationsJson);

                    var newPages = await _context.WBPages
                        .Where(p => p.SiteId == duplicated.Id && !p.IsDeleted)
                        .ToListAsync();

                    foreach (var newPage in newPages)
                    {
                        if (originalPagesBySlug.TryGetValue(newPage.Slug, out var translationsJson))
                        {
                            newPage.TranslationsJson = translationsJson;
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                return (await GetSiteByIdAsync(duplicated.Id)) ?? duplicated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error duplicating WB site {SiteId}", id);
                throw;
            }
        }

        public async Task<WBSiteResponseDto?> PublishSiteAsync(int id, string publishedByUser)
        {
            try
            {
                var site = await _context.WBSites
                    .Where(s => s.Id == id && !s.IsDeleted)
                    .FirstOrDefaultAsync();

                if (site == null) return null;

                var pages = await _context.WBPages
                    .Where(p => p.SiteId == site.Id && !p.IsDeleted)
                    .ToListAsync();

                var now = DateTime.UtcNow;

                // ── Wave 2: atomic publish snapshot ──
                // Freeze every live page's content into its Published* columns
                // inside a single transaction. The public renderer reads from
                // these columns, so the live site only flips when EVERY page
                // has been snapshotted successfully. Falls back to retry-safe
                // execution strategy for EnableRetryOnFailure.
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        foreach (var p in pages)
                        {
                            p.PublishedComponentsJson = p.ComponentsJson;
                            p.PublishedSeoJson = p.SeoJson;
                            p.PublishedTranslationsJson = p.TranslationsJson;
                            p.PublishedAt = now;
                        }

                        site.Published = true;
                        site.PublishedAt = now;
                        site.PublishedUrl = $"/public/sites/{site.Slug}";
                        site.ModifiedBy = publishedByUser;
                        site.UpdatedAt = now;

                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();
                    }
                    catch
                    {
                        await tx.RollbackAsync();
                        throw;
                    }
                });

                await _activityLog.LogActivityAsync(site.Id, null, "publish", "site",
                    $"Site '{site.Name}' published ({pages.Count} pages snapshotted)", publishedByUser);

                return await GetSiteByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing WB site {SiteId}", id);
                throw;
            }
        }

        public async Task<WBSiteResponseDto?> UnpublishSiteAsync(int id, string modifiedByUser)
        {
            try
            {
                var site = await _context.WBSites
                    .Where(s => s.Id == id && !s.IsDeleted)
                    .FirstOrDefaultAsync();

                if (site == null) return null;

                site.Published = false;
                site.PublishedUrl = null;
                site.ModifiedBy = modifiedByUser;
                site.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await _activityLog.LogActivityAsync(site.Id, null, "unpublish", "site", $"Site '{site.Name}' unpublished", modifiedByUser);

                return await GetSiteByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpublishing WB site {SiteId}", id);
                throw;
            }
        }

        // ── Helpers ──

        private static WBSiteResponseDto MapToSiteDto(WBSite site, bool publicSnapshot = false)
        {
            return new WBSiteResponseDto
            {
                Id = site.Id,
                Name = site.Name,
                Slug = site.Slug,
                Description = site.Description,
                Favicon = site.Favicon,
                ThemeJson = site.ThemeJson,
                Published = site.Published,
                PublishedAt = site.PublishedAt,
                PublishedUrl = site.PublishedUrl,
                DefaultLanguage = site.DefaultLanguage,
                LanguagesJson = site.LanguagesJson,
                CreatedAt = site.CreatedAt,
                UpdatedAt = site.UpdatedAt,
                CreatedBy = site.CreatedBy,
                ModifiedBy = site.ModifiedBy,
                Pages = site.Pages.Select(p => new WBPageResponseDto
                {
                    Id = p.Id,
                    SiteId = p.SiteId,
                    Title = p.Title,
                    Slug = p.Slug,
                    // ── Wave 2: when serving the public renderer, return the
                    // frozen Published* snapshot so editor saves never leak
                    // into the live site mid-edit. Fall back to live values
                    // for legacy pages that haven't been re-published yet.
                    ComponentsJson = publicSnapshot
                        ? (p.PublishedComponentsJson ?? p.ComponentsJson)
                        : p.ComponentsJson,
                    SeoJson = publicSnapshot
                        ? (p.PublishedSeoJson ?? p.SeoJson)
                        : p.SeoJson,
                    TranslationsJson = publicSnapshot
                        ? (p.PublishedTranslationsJson ?? p.TranslationsJson)
                        : p.TranslationsJson,
                    IsHomePage = p.IsHomePage,
                    SortOrder = p.SortOrder,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    CreatedBy = p.CreatedBy,
                    PublishedAt = p.PublishedAt,
                }).OrderBy(p => p.SortOrder).ToList()
            };
        }

        private static string GenerateSlug(string text)
        {
            return System.Text.RegularExpressions.Regex
                .Replace(text.ToLower(), @"[^a-z0-9]+", "-")
                .Trim('-');
        }
    }
}
