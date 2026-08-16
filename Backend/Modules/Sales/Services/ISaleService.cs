using MyApi.Modules.Sales.DTOs;

namespace MyApi.Modules.Sales.Services
{
    public interface ISaleService
    {
        Task<PaginatedSaleResponse> GetSalesAsync(
            string? status = null,
            string? stage = null,
            string? priority = null,
            string? contactId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? search = null,
            int page = 1,
            int limit = 20,
            string sortBy = "updated_at",
            string sortOrder = "desc"
        );
        
        Task<SaleDto?> GetSaleByIdAsync(int id);
        Task<SaleDto> CreateSaleAsync(CreateSaleDto createDto, string userId);
        Task<SaleDto> CreateSaleFromOfferAsync(int offerId, string userId);
        Task<SaleDto> UpdateSaleAsync(int id, UpdateSaleDto updateDto, string userId);
        Task<bool> DeleteSaleAsync(int id, string userId = "system");
        Task<SaleStatsDto> GetSaleStatsAsync(DateTime? dateFrom = null, DateTime? dateTo = null);
        
        // Sale Items
        Task<SaleItemDto> AddSaleItemAsync(int saleId, CreateSaleItemDto itemDto);
        Task<SaleItemDto> UpdateSaleItemAsync(int saleId, int itemId, CreateSaleItemDto itemDto);
        Task<bool> DeleteSaleItemAsync(int saleId, int itemId);
        
        // Sale Activities
        Task<List<SaleActivityDto>> GetSaleActivitiesAsync(int saleId, string? type = null, int page = 1, int limit = 20);
    }
}
