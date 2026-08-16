using MyApi.Modules.Deals.DTOs;

namespace MyApi.Modules.Deals.Services
{
    public interface IDealService
    {
        Task<PaginatedDealResponse> GetDealsAsync(
            string? stage = null,
            string? category = null,
            string? source = null,
            string? contactId = null,
            string? projectId = null,
            string? search = null,
            int page = 1,
            int limit = 20,
            string sortBy = "updated_at",
            string sortOrder = "desc");

        Task<DealDto?> GetDealByIdAsync(int id);
        Task<DealDto> CreateDealAsync(CreateDealDto createDto, string userId, string? userName = null);
        Task<DealDto?> UpdateDealAsync(int id, UpdateDealDto updateDto, string userId, string? userName = null);
        Task<bool> DeleteDealAsync(int id, string userId = "system");

        Task<DealStatsDto> GetDealStatsAsync(DateTime? dateFrom = null, DateTime? dateTo = null);

        Task<ConvertDealResultDto> ConvertDealAsync(int id, ConvertDealDto convertDto, string userId, string? userName = null);

        // Items
        Task<DealItemDto?> AddDealItemAsync(int dealId, CreateDealItemDto itemDto, string userId = "system", string? userName = null);
        Task<DealItemDto?> UpdateDealItemAsync(int dealId, int itemId, CreateDealItemDto itemDto, string userId = "system", string? userName = null);
        Task<bool> DeleteDealItemAsync(int dealId, int itemId, string userId = "system", string? userName = null);

        // Activities
        Task<(List<DealActivityDto> Items, int Total)> GetDealActivitiesAsync(int dealId, string? type = null, int page = 1, int limit = 20);
        Task<DealActivityDto?> AddDealActivityAsync(int dealId, CreateDealActivityDto activityDto, string userId, string? userName = null);
        Task<bool> DeleteDealActivityAsync(int dealId, int activityId, string userId = "system", string? userName = null);
    }
}
