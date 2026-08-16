using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Contacts.DTOs;
using MyApi.Modules.Contacts.Models;

namespace MyApi.Modules.Contacts.Services
{
    public class ContactActivityService : IContactActivityService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ContactActivityService> _logger;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        public ContactActivityService(ApplicationDbContext context, ILogger<ContactActivityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(
            int contactId,
            string type,
            string? relatedEntityType = null,
            int? relatedEntityId = null,
            string? description = null,
            object? metadata = null,
            string? createdBy = null)
        {
            if (contactId <= 0 || string.IsNullOrWhiteSpace(type))
                return;

            try
            {
                var entry = new ContactActivity
                {
                    ContactId = contactId,
                    Type = type,
                    RelatedEntityType = relatedEntityType,
                    RelatedEntityId = relatedEntityId,
                    Description = description,
                    Metadata = metadata != null ? JsonSerializer.Serialize(metadata, JsonOpts) : null,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = createdBy,
                };

                _context.ContactActivities.Add(entry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Never let audit-log failures break the primary business operation.
                _logger.LogWarning(ex,
                    "Failed to log contact activity ContactId={ContactId} Type={Type}",
                    contactId, type);
            }
        }

        public async Task<ContactActivityListResponseDto> GetByContactIdAsync(int contactId, int page = 1, int pageSize = 100)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 100;

            var query = _context.ContactActivities
                .AsNoTracking()
                .Where(a => a.ContactId == contactId);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new ContactActivityDto
                {
                    Id = a.Id,
                    ContactId = a.ContactId,
                    Type = a.Type,
                    RelatedEntityType = a.RelatedEntityType,
                    RelatedEntityId = a.RelatedEntityId,
                    Description = a.Description,
                    Metadata = a.Metadata,
                    CreatedAt = a.CreatedAt,
                    CreatedBy = a.CreatedBy,
                })
                .ToListAsync();

            return new ContactActivityListResponseDto
            {
                Activities = items,
                TotalCount = total,
            };
        }
    }
}
