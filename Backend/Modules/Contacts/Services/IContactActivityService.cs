using MyApi.Modules.Contacts.DTOs;

namespace MyApi.Modules.Contacts.Services
{
    public interface IContactActivityService
    {
        /// <summary>
        /// Log an activity entry against a contact. Safe to call from other module
        /// services — swallows exceptions so a logging failure never breaks the
        /// caller's primary business operation.
        /// </summary>
        Task LogAsync(
            int contactId,
            string type,
            string? relatedEntityType = null,
            int? relatedEntityId = null,
            string? description = null,
            object? metadata = null,
            string? createdBy = null);

        Task<ContactActivityListResponseDto> GetByContactIdAsync(int contactId, int page = 1, int pageSize = 100);
    }
}
