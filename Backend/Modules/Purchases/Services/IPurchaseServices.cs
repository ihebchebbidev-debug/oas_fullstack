using MyApi.Modules.Purchases.DTOs;

namespace MyApi.Modules.Purchases.Services
{
    public interface IPurchaseOrderService
    {
        Task<PaginatedPurchaseOrderResponse> GetOrdersAsync(
            string? status = null, string? supplierId = null, string? paymentStatus = null,
            DateTime? dateFrom = null, DateTime? dateTo = null, string? search = null,
            int page = 1, int limit = 20, string sortBy = "created_date", string sortOrder = "desc");
        Task<PurchaseOrderDto?> GetOrderByIdAsync(int id);
        Task<PurchaseOrderDto> CreateOrderAsync(CreatePurchaseOrderDto dto, string userId, string? userName = null, string? idempotencyKey = null);
        Task<PurchaseOrderDto> UpdateOrderAsync(int id, UpdatePurchaseOrderDto dto, string userId, string? userName = null);
        Task<bool> DeleteOrderAsync(int id, string userId, string? userName = null);
        Task<PurchaseOrderStatsDto> GetStatsAsync(DateTime? dateFrom = null, DateTime? dateTo = null);
        // Items
        Task<PurchaseOrderItemDto> AddItemAsync(int orderId, CreatePurchaseOrderItemDto dto, string? userId = null, string? userName = null);
        Task<PurchaseOrderItemDto> UpdateItemAsync(int orderId, int itemId, CreatePurchaseOrderItemDto dto, string? userId = null, string? userName = null);
        Task<bool> DeleteItemAsync(int orderId, int itemId, string? userId = null, string? userName = null);
        // Activities
        Task<List<PurchaseActivityDto>> GetActivitiesAsync(int orderId, int page = 1, int limit = 20);
        Task<PaginatedPurchaseActivityResponse> GetAllActivitiesAsync(
            string? entityType = null, int? entityId = null, string? activityType = null,
            string? search = null, DateTime? dateFrom = null, DateTime? dateTo = null,
            int page = 1, int limit = 50);
    }

    public interface IGoodsReceiptService
    {
        Task<PaginatedGoodsReceiptResponse> GetReceiptsAsync(
            int? purchaseOrderId = null, string? supplierId = null, string? status = null,
            DateTime? dateFrom = null, DateTime? dateTo = null, string? search = null,
            int page = 1, int limit = 20, string sortBy = "created_date", string sortOrder = "desc");
        Task<GoodsReceiptDto?> GetReceiptByIdAsync(int id);
        Task<GoodsReceiptDto> CreateReceiptAsync(CreateGoodsReceiptDto dto, string userId, string? userName = null, string? idempotencyKey = null);
        Task<GoodsReceiptDto> UpdateReceiptAsync(int id, UpdateGoodsReceiptDto dto, string userId, string? userName = null);
        Task<bool> DeleteReceiptAsync(int id, string userId, string? userName = null);
        Task<List<PurchaseActivityDto>> GetActivitiesAsync(int receiptId, int page = 1, int limit = 50);
    }

    public interface ISupplierInvoiceService
    {
        Task<PaginatedSupplierInvoiceResponse> GetInvoicesAsync(
            string? status = null, string? supplierId = null, bool? rsApplicable = null,
            DateTime? dateFrom = null, DateTime? dateTo = null, string? search = null,
            int page = 1, int limit = 20, string sortBy = "created_date", string sortOrder = "desc",
            bool? overdueOnly = null);
        Task<SupplierInvoiceDto?> GetInvoiceByIdAsync(int id);
        Task<SupplierInvoiceDto> CreateInvoiceAsync(CreateSupplierInvoiceDto dto, string userId, string? userName = null, string? idempotencyKey = null);
        Task<SupplierInvoiceDto> UpdateInvoiceAsync(int id, UpdateSupplierInvoiceDto dto, string userId, string? userName = null);
        Task<bool> DeleteInvoiceAsync(int id, string userId, string? userName = null);
        // Items
        Task<SupplierInvoiceItemDto> AddItemAsync(int invoiceId, CreateSupplierInvoiceItemDto dto, string? userId = null, string? userName = null);
        Task<SupplierInvoiceItemDto> UpdateItemAsync(int invoiceId, int itemId, CreateSupplierInvoiceItemDto dto, string? userId = null, string? userName = null);
        Task<bool> DeleteItemAsync(int invoiceId, int itemId, string? userId = null, string? userName = null);
        // Activities
        Task<List<PurchaseActivityDto>> GetActivitiesAsync(int invoiceId, int page = 1, int limit = 50);
    }

    public interface IArticleSupplierService
    {
        Task<List<ArticleSupplierDto>> GetByArticleAsync(int articleId);
        Task<List<ArticleSupplierDto>> GetBySupplierAsync(int supplierId);
        Task<ArticleSupplierDto?> GetByIdAsync(int id);
        Task<ArticleSupplierDto> CreateAsync(CreateArticleSupplierDto dto, string userId);
        Task<ArticleSupplierDto> UpdateAsync(int id, UpdateArticleSupplierDto dto, string userId);
        Task<bool> DeleteAsync(int id, string userId);
        Task<List<ArticleSupplierPriceHistoryDto>> GetPriceHistoryAsync(int articleSupplierId);
    }
}
