using MyApi.Modules.Shared.DTOs;

namespace MyApi.Modules.Shared.Services
{
    /// <summary>
    /// Service interface for Entity Form Document operations
    /// </summary>
    public interface IEntityFormDocumentService
    {
        /// <summary>
        /// Get all form documents for a specific entity (offer/sale)
        /// </summary>
        Task<IEnumerable<EntityFormDocumentDto>> GetByEntityAsync(string entityType, int entityId);

        /// <summary>
        /// Get a single form document by ID
        /// </summary>
        Task<EntityFormDocumentDto?> GetByIdAsync(int id);

        /// <summary>
        /// Create a new form document
        /// </summary>
        Task<EntityFormDocumentDto> CreateAsync(CreateEntityFormDocumentDto dto, string userId);

        /// <summary>
        /// Update an existing form document
        /// </summary>
        Task<EntityFormDocumentDto> UpdateAsync(int id, UpdateEntityFormDocumentDto dto, string userId);

        /// <summary>
        /// Delete a form document (soft delete)
        /// </summary>
    Task<bool> DeleteAsync(int id, string userId);
    
    /// <summary>
    /// Copy all form documents from one entity to another (e.g., from offer to sale during conversion)
    /// </summary>
    Task<int> CopyDocumentsToEntityAsync(string sourceEntityType, int sourceEntityId, string targetEntityType, int targetEntityId, string userId);

    /// <summary>
    /// Copy item-level checklists along the conversion lineage
    /// (offer_item → sale_item → service_order_job). Idempotent (skips non-empty targets).
    /// </summary>
    Task<int> CopyItemDocumentsAsync(string sourceEntityType, int sourceEntityId, string targetEntityType, int targetEntityId, string userId);
}
}
