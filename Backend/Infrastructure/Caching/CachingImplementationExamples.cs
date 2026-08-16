using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.Contacts.Models;

namespace MyApi.Infrastructure.Caching
{
    /// <summary>
    /// Example service demonstrating caching patterns for ContactTag operations.
    /// </summary>
    public class ContactTagServiceWithCaching
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cacheService;
        private readonly CacheInvalidationHelper _cacheInvalidationHelper;
        private readonly ILogger<CacheInvalidationHelper> _logger;

        private const string CachePrefix = "contact_tags";

        public ContactTagServiceWithCaching(
            ApplicationDbContext context,
            ICacheService cacheService,
            CacheInvalidationHelper cacheInvalidationHelper,
            ILogger<CacheInvalidationHelper> logger)
        {
            _context = context;
            _cacheService = cacheService;
            _cacheInvalidationHelper = cacheInvalidationHelper;
            _logger = logger;
        }

        /// <summary>
        /// Get all active tags with caching
        /// </summary>
        public async Task<List<ContactTag>> GetAllTagsAsync()
        {
            var cacheKey = $"{CachePrefix}_all";
            var tags = await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                return await _context.ContactTags
                    .Where(t => !t.IsDeleted)
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }, TimeSpan.FromMinutes(30));

            return tags ?? new List<ContactTag>();
        }

        /// <summary>
        /// Get tag by ID with caching
        /// </summary>
        public async Task<ContactTag?> GetTagByIdAsync(int id)
        {
            var cacheKey = $"{CachePrefix}_{id}";
            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                return await _context.ContactTags
                    .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
            }, TimeSpan.FromMinutes(30));
        }

        /// <summary>
        /// Create a new tag and invalidate cache
        /// </summary>
        public async Task<ContactTag> CreateTagAsync(string name, string? color, string createdBy)
        {
            var tag = new ContactTag
            {
                Name = name,
                Color = color ?? "#3b82f6",
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.ContactTags.Add(tag);
            await _context.SaveChangesAsync();

            // Invalidate list cache
            _cacheService.RemoveByPattern($"{CachePrefix}_*");
            _logger.LogInformation("Created tag {TagId} '{TagName}' and invalidated cache", tag.Id, tag.Name);

            return tag;
        }

        /// <summary>
        /// Soft-delete a tag
        /// </summary>
        public async Task<bool> DeleteTagAsync(int id)
        {
            var tag = await _context.ContactTags.FindAsync(id);
            if (tag == null) return false;

            tag.IsDeleted = true;
            await _context.SaveChangesAsync();

            _cacheService.RemoveByPattern($"{CachePrefix}_*");
            _logger.LogInformation("Soft-deleted tag {TagId} and invalidated cache", id);

            return true;
        }

        /// <summary>
        /// Assign a tag to a contact
        /// </summary>
        public async Task<ContactTagAssignment> AssignTagAsync(int contactId, int tagId, string? assignedBy)
        {
            var assignment = new ContactTagAssignment
            {
                ContactId = contactId,
                TagId = tagId,
                AssignedDate = DateTime.UtcNow,
                AssignedBy = assignedBy
            };

            _context.ContactTagAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            _cacheInvalidationHelper.InvalidateContactCaches();
            _logger.LogInformation("Assigned tag {TagId} to contact {ContactId}", tagId, contactId);

            return assignment;
        }

        /// <summary>
        /// Search tags by name with caching
        /// </summary>
        public async Task<List<ContactTag>> SearchTagsAsync(string searchTerm)
        {
            var cacheKey = $"{CachePrefix}_search_{searchTerm.ToLower()}";
            var tags = await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                return await _context.ContactTags
                    .Where(t => !t.IsDeleted &&
                        EF.Functions.ILike(t.Name, $"%{searchTerm}%"))
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }, TimeSpan.FromMinutes(5));

            return tags ?? new List<ContactTag>();
        }

        /// <summary>
        /// Get tags for a specific contact
        /// </summary>
        public async Task<List<ContactTag>> GetTagsForContactAsync(int contactId)
        {
            var cacheKey = $"{CachePrefix}_contact_{contactId}";
            var tags = await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                return await _context.ContactTagAssignments
                    .Where(a => a.ContactId == contactId)
                    .Select(a => a.Tag!)
                    .Where(t => !t.IsDeleted)
                    .ToListAsync();
            }, TimeSpan.FromMinutes(15));

            return tags ?? new List<ContactTag>();
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStats GetCacheStats()
        {
            return _cacheService.GetStats();
        }
    }
}
