using MyApi.Modules.Articles.DTOs;

namespace MyApi.Modules.Articles.Services
{
    public interface IStockTransactionService
    {
        // Get transactions for an article
        Task<StockTransactionListDto> GetTransactionsAsync(StockTransactionSearchDto? searchDto = null);
        Task<List<StockTransactionDto>> GetArticleTransactionsAsync(int articleId, int limit = 50);
        Task<StockTransactionDto?> GetTransactionByIdAsync(int id);
        
        // Create transaction (with stock update)
        Task<StockTransactionDto> CreateTransactionAsync(CreateStockTransactionDto dto, string userId, string? userName = null, string? ipAddress = null);
        
        // Manual stock adjustment - added ipAddress parameter
        Task<StockTransactionDto> AddStockAsync(int articleId, decimal quantity, string reason, string userId, string? userName = null, string? notes = null, string? ipAddress = null);
        Task<StockTransactionDto> RemoveStockAsync(int articleId, decimal quantity, string reason, string userId, string? userName = null, string? notes = null, string? ipAddress = null);
        
        // Sale-related stock operations - added ipAddress parameter
        Task<StockDeductionResultDto> DeductStockFromSaleAsync(int saleId, string userId, string? userName = null, string? ipAddress = null);
        Task<StockDeductionResultDto> RestoreStockFromSaleAsync(int saleId, string userId, string? userName = null, string? ipAddress = null);
        
        // Offer tracking (logs only, no stock change)
        Task<StockTransactionDto> LogOfferItemAddedAsync(int articleId, decimal quantity, int offerId, string offerNumber, string userId, string? userName = null);
    }
}
