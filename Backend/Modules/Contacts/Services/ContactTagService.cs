using MyApi.Data;
using MyApi.Modules.Contacts.DTOs;
using MyApi.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using MyApi.Infrastructure.Caching;

namespace MyApi.Modules.Contacts.Services
{
    public class ContactTagService : IContactTagService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ContactTagService> _logger;
        private readonly ICacheService _cache;
        // Tenant-scoped: a shared key leaked one tenant's tags to every other tenant.
        private string TagsCacheKey => $"contact_tags_all:{_context.GetTenantId()}";
        private static readonly TimeSpan TagCacheDuration = TimeSpan.FromMinutes(30);

        public ContactTagService(ApplicationDbContext context, ILogger<ContactTagService> logger, ICacheService cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        public async Task<ContactTagListResponseDto> GetAllTagsAsync()
        {
            try
            {
                return await _cache.GetOrSetAsync(TagsCacheKey, async () =>
                {
                    var tags = await _context.ContactTags
                        .AsNoTracking()
                        .Where(t => !t.IsDeleted)
                        .Include(t => t.ContactAssignments)
                        .OrderBy(t => t.Name)
                        .ToListAsync();

                    var tagDtos = tags.Select(MapToTagDto).ToList();

                    return new ContactTagListResponseDto
                    {
                        Tags = tagDtos,
                        TotalCount = tagDtos.Count
                    };
                }, TagCacheDuration) ?? new ContactTagListResponseDto { Tags = new(), TotalCount = 0 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all contact tags");
                throw;
            }
        }

        public async Task<ContactTagDto?> GetTagByIdAsync(int id)
        {
            try
            {
                var tag = await _context.ContactTags
                    .AsNoTracking()
                    .Include(t => t.ContactAssignments)
                    .Where(t => t.Id == id && !t.IsDeleted)
                    .FirstOrDefaultAsync();

                return tag != null ? MapToTagDto(tag) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contact tag by id {TagId}", id);
                throw;
            }
        }

        public async Task<ContactTagDto> CreateTagAsync(CreateContactTagRequestDto createDto)
        {
            try
            {
                var existingTag = await _context.ContactTags
                    .Where(t => t.Name.ToLower() == createDto.Name.ToLower() && !t.IsDeleted)
                    .FirstOrDefaultAsync();

                if (existingTag != null)
                {
                    throw new InvalidOperationException("A tag with this name already exists");
                }

                var tag = new ContactTag
                {
                    Name = createDto.Name,
                    Color = createDto.Color,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "system"
                };

                _context.ContactTags.Add(tag);
                await _context.SaveChangesAsync();

                // ✅ Invalidate cache
                _cache.Remove(TagsCacheKey);

                _logger.LogInformation("Contact tag created successfully with ID {TagId}", tag.Id);
                return MapToTagDto(tag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating contact tag");
                throw;
            }
        }

        public async Task<ContactTagDto?> UpdateTagAsync(int id, UpdateContactTagRequestDto updateDto)
        {
            try
            {
                var tag = await _context.ContactTags
                    .Where(t => t.Id == id && !t.IsDeleted)
                    .FirstOrDefaultAsync();

                if (tag == null)
                {
                    return null;
                }

                if (!string.IsNullOrEmpty(updateDto.Name) && 
                    updateDto.Name.ToLower() != tag.Name.ToLower())
                {
                    var existingTag = await _context.ContactTags
                        .Where(t => t.Name.ToLower() == updateDto.Name.ToLower() && t.Id != id && !t.IsDeleted)
                        .FirstOrDefaultAsync();

                    if (existingTag != null)
                    {
                        throw new InvalidOperationException("A tag with this name already exists");
                    }

                    tag.Name = updateDto.Name;
                }

                if (!string.IsNullOrEmpty(updateDto.Color))
                    tag.Color = updateDto.Color;

                await _context.SaveChangesAsync();

                // ✅ Invalidate cache
                _cache.Remove(TagsCacheKey);

                _logger.LogInformation("Contact tag updated successfully with ID {TagId}", id);
                
                var updatedTag = await GetTagByIdAsync(id);
                return updatedTag;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact tag with ID {TagId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteTagAsync(int id)
        {
            try
            {
                var tag = await _context.ContactTags
                    .Where(t => t.Id == id && !t.IsDeleted)
                    .FirstOrDefaultAsync();

                if (tag == null)
                {
                    return false;
                }

                var assignments = await _context.Set<ContactTagAssignment>()
                    .Where(ta => ta.TagId == id)
                    .ToListAsync();

                // Soft delete: keep the row so historical references (activity log,
                // reports) can still resolve the tag name/colour.
                _context.Set<ContactTagAssignment>().RemoveRange(assignments);
                tag.IsDeleted = true;

                await _context.SaveChangesAsync();

                // ✅ Invalidate cache
                _cache.Remove(TagsCacheKey);

                _logger.LogInformation("Contact tag deleted successfully with ID {TagId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact tag with ID {TagId}", id);
                throw;
            }
        }

        public async Task<bool> TagExistsAsync(string name)
        {
            try
            {
                return await _context.ContactTags
                    .AnyAsync(t => t.Name.ToLower() == name.ToLower() && !t.IsDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if contact tag exists with name {TagName}", name);
                throw;
            }
        }

        private static ContactTagDto MapToTagDto(ContactTag tag)
        {
            return new ContactTagDto
            {
                Id = tag.Id,
                Name = tag.Name,
                Color = tag.Color,
                CreatedDate = tag.CreatedDate,
                CreatedBy = tag.CreatedBy,
                ContactCount = tag.ContactAssignments?.Count ?? 0
            };
        }
    }
}
