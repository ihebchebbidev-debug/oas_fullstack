using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MyApi.Modules.Articles.Services;
using MyApi.Modules.Articles.DTOs;

namespace MyApi.Modules.Articles.Controllers
{
    [ApiController]
    [Route("api/stock-transactions")]
    [Authorize]
    public class StockTransactionController : ControllerBase
    {
        private readonly IStockTransactionService _stockTransactionService;
        private readonly ILogger<StockTransactionController> _logger;

        public StockTransactionController(
            IStockTransactionService stockTransactionService,
            ILogger<StockTransactionController> logger)
        {
            _stockTransactionService = stockTransactionService;
            _logger = logger;
        }

        /// <summary>
        /// Get all stock transactions with optional filters
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<StockTransactionListDto>> GetTransactions(
            [FromQuery] int? articleId = null,
            [FromQuery] string? transactionType = null,
            [FromQuery] string? referenceType = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string sortBy = "created_at",
            [FromQuery] string sortOrder = "desc")
        {
            try
            {
                var searchDto = new StockTransactionSearchDto
                {
                    ArticleId = articleId,
                    TransactionType = transactionType,
                    ReferenceType = referenceType,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    PerformedBy = performedBy,
                    Page = page,
                    Limit = limit,
                    SortBy = sortBy,
                    SortOrder = sortOrder
                };

                var result = await _stockTransactionService.GetTransactionsAsync(searchDto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stock transactions");
                return StatusCode(500, new { error = "Failed to get stock transactions" });
            }
        }

        /// <summary>
        /// Get transactions for a specific article
        /// </summary>
        [HttpGet("article/{articleId}")]
        public async Task<ActionResult<List<StockTransactionDto>>> GetArticleTransactions(
            int articleId,
            [FromQuery] int limit = 50)
        {
            try
            {
                var transactions = await _stockTransactionService.GetArticleTransactionsAsync(articleId, limit);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions for article {ArticleId}", articleId);
                return StatusCode(500, new { error = "Failed to get article transactions" });
            }
        }

        /// <summary>
        /// Get a specific transaction by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<StockTransactionDto>> GetTransaction(int id)
        {
            try
            {
                var transaction = await _stockTransactionService.GetTransactionByIdAsync(id);
                if (transaction == null)
                    return NotFound(new { error = "Transaction not found" });

                return Ok(transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction {TransactionId}", id);
                return StatusCode(500, new { error = "Failed to get transaction" });
            }
        }

        /// <summary>
        /// Add stock to an article
        /// </summary>
        [HttpPost("add")]
        public async Task<ActionResult<StockTransactionDto>> AddStock([FromBody] AddRemoveStockRequest request)
        {
            try
            {
                // Validate request
                if (request.Quantity <= 0)
                {
                    return BadRequest(new { error = "Quantity must be greater than zero" });
                }

                var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value ?? 
                             User.FindFirst("UserId")?.Value ?? "system";
                var userName = User.FindFirst("name")?.Value ?? 
                              User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
                var ipAddress = GetClientIpAddress();

                var transaction = await _stockTransactionService.AddStockAsync(
                    request.ArticleId,
                    request.Quantity,
                    request.Reason,
                    userId,
                    userName,
                    request.Notes,
                    ipAddress);  // Now passing IP address

                return Ok(transaction);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding stock to article {ArticleId}", request.ArticleId);
                return StatusCode(500, new { error = "Failed to add stock" });
            }
        }

        /// <summary>
        /// Remove stock from an article
        /// </summary>
        [HttpPost("remove")]
        public async Task<ActionResult<StockTransactionDto>> RemoveStock([FromBody] AddRemoveStockRequest request)
        {
            try
            {
                // Validate request
                if (request.Quantity <= 0)
                {
                    return BadRequest(new { error = "Quantity must be greater than zero" });
                }

                var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value ?? 
                             User.FindFirst("UserId")?.Value ?? "system";
                var userName = User.FindFirst("name")?.Value ?? 
                              User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
                var ipAddress = GetClientIpAddress();

                var transaction = await _stockTransactionService.RemoveStockAsync(
                    request.ArticleId,
                    request.Quantity,
                    request.Reason,
                    userId,
                    userName,
                    request.Notes,
                    ipAddress);  // Now passing IP address

                return Ok(transaction);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient stock"))
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing stock from article {ArticleId}", request.ArticleId);
                return StatusCode(500, new { error = "Failed to remove stock" });
            }
        }

        /// <summary>
        /// Deduct stock when a sale is closed
        /// </summary>
        [HttpPost("deduct-from-sale/{saleId}")]
        public async Task<ActionResult<StockDeductionResultDto>> DeductStockFromSale(int saleId)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value ?? 
                             User.FindFirst("UserId")?.Value ?? "system";
                var userName = User.FindFirst("name")?.Value ?? 
                              User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
                var ipAddress = GetClientIpAddress();

                var result = await _stockTransactionService.DeductStockFromSaleAsync(saleId, userId, userName, ipAddress);
                
                if (!result.Success)
                {
                    return BadRequest(result);
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deducting stock from sale {SaleId}", saleId);
                return StatusCode(500, new { error = "Failed to deduct stock from sale" });
            }
        }

        /// <summary>
        /// Restore stock when a sale is cancelled/reopened
        /// </summary>
        [HttpPost("restore-from-sale/{saleId}")]
        public async Task<ActionResult<StockDeductionResultDto>> RestoreStockFromSale(int saleId)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value ?? 
                             User.FindFirst("UserId")?.Value ?? "system";
                var userName = User.FindFirst("name")?.Value ?? 
                              User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
                var ipAddress = GetClientIpAddress();

                var result = await _stockTransactionService.RestoreStockFromSaleAsync(saleId, userId, userName, ipAddress);
                
                if (!result.Success)
                {
                    return BadRequest(result);
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring stock from sale {SaleId}", saleId);
                return StatusCode(500, new { error = "Failed to restore stock from sale" });
            }
        }

        /// <summary>
        /// Get client IP address considering proxies
        /// </summary>
        private string? GetClientIpAddress()
        {
            // Check for forwarded headers first (behind proxy/load balancer)
            var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Take the first IP if multiple are present
                return forwardedFor.Split(',').FirstOrDefault()?.Trim();
            }

            var realIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }

    public class AddRemoveStockRequest
    {
        public int ArticleId { get; set; }
        public decimal Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
