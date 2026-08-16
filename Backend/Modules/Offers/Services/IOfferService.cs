using MyApi.Modules.Offers.DTOs;

namespace MyApi.Modules.Offers.Services
{
    public interface IOfferService
    {
        Task<PaginatedOfferResponse> GetOffersAsync(
            string? status = null,
            string? category = null,
            string? source = null,
            string? contactId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? search = null,
            int page = 1,
            int limit = 20,
            string sortBy = "updated_at",
            string sortOrder = "desc"
        );
        
        Task<OfferDto?> GetOfferByIdAsync(int id);
        Task<OfferDto> CreateOfferAsync(CreateOfferDto createDto, string userId);
        Task<OfferDto> UpdateOfferAsync(int id, UpdateOfferDto updateDto, string userId);
        Task<bool> DeleteOfferAsync(int id, string userId);
        Task<OfferDto> RenewOfferAsync(int id, string userId);
        Task<object> ConvertOfferAsync(int id, ConvertOfferDto convertDto, string userId);
        Task<OfferStatsDto> GetOfferStatsAsync(DateTime? dateFrom = null, DateTime? dateTo = null);
        Task<OfferDto> MarkOfferAsSentAsync(int id, string userId);
        
        // Offer Items
        Task<OfferItemDto> AddOfferItemAsync(int offerId, CreateOfferItemDto itemDto);
        Task<OfferItemDto> UpdateOfferItemAsync(int offerId, int itemId, CreateOfferItemDto itemDto);
        Task<bool> DeleteOfferItemAsync(int offerId, int itemId);
        
        // Offer Activities
        Task<List<OfferActivityDto>> GetOfferActivitiesAsync(int offerId, string? type = null, int page = 1, int limit = 20);
        Task<OfferActivityDto> AddOfferActivityAsync(int offerId, CreateOfferActivityDto activityDto, string userId);
        Task<bool> DeleteOfferActivityAsync(int offerId, int activityId);
        
        /// <summary>
        /// High-performance bulk import supporting up to 10,000+ offers with batch processing.
        /// </summary>
        Task<BulkImportOfferResultDto> BulkImportOffersAsync(BulkImportOfferRequestDto importRequest, string userId);
    }
}
