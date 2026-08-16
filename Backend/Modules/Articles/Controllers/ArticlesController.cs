using System.Threading.Tasks;
using System.Security.Claims;
using MyApi.Modules.Articles.DTOs;
using MyApi.Modules.Articles.Services;
using MyApi.Modules.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace MyApi.Modules.Articles.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/articles")]
    public class ArticlesController : ControllerBase
    {
        private readonly IArticleService _articleService;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<ArticlesController> _logger;

        public ArticlesController(
            IArticleService articleService, 
            ISystemLogService systemLogService,
            ILogger<ArticlesController> logger)
        {
            _articleService = articleService;
            _systemLogService = systemLogService;
            _logger = logger;
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        }

        private string GetCurrentUserName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst("FirstName")?.Value + " " + User.FindFirst("LastName")?.Value ?? 
                   User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   "anonymous";
        }

        // =====================================================
        // Article Endpoints
        // =====================================================

        /// <summary>
        /// Get all articles with optional filtering and pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ArticleListDto>> GetAllArticles(
            [FromQuery] string? type = null,
            [FromQuery] string? category = null,
            [FromQuery] string? status = null,
            [FromQuery] string? location = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = "asc")
        {
            var result = await _articleService.GetAllArticlesAsync(
                type, category, status, location, search, page, limit, sortBy, sortOrder);
            
            return Ok(result);
        }

        /// <summary>
        /// Get article by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ArticleDto>> GetArticleById(string id)
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            
            if (article == null)
                return NotFound(new { message = "Article not found" });
            
            return Ok(article);
        }

        /// <summary>
        /// Create a new article
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ArticleDto>> CreateArticle([FromBody] CreateArticleDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var article = await _articleService.CreateArticleAsync(dto, userId);

                await _systemLogService.LogSuccessAsync($"Article created: {dto.Name}", "Articles", "create", userId, GetCurrentUserName(), "Article", article.Id.ToString());
                
                return CreatedAtAction(nameof(GetArticleById), new { id = article.Id }, new
                {
                    success = true,
                    data = article
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating article");
                await _systemLogService.LogErrorAsync("Failed to create article", "Articles", "create", GetCurrentUserId(), GetCurrentUserName(), "Article", details: ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = "INTERNAL_ERROR",
                        message = "An error occurred while creating the article"
                    }
                });
            }
        }

        /// <summary>
        /// Update an existing article
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ArticleDto>> UpdateArticle(string id, [FromBody] UpdateArticleDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var article = await _articleService.UpdateArticleAsync(id, dto, userId);
                
                if (article == null)
                    return NotFound(new
                    {
                        success = false,
                        error = new
                        {
                            code = "ARTICLE_NOT_FOUND",
                            message = "Article not found"
                        }
                    });

                await _systemLogService.LogSuccessAsync($"Article updated: {article.Name}", "Articles", "update", userId, GetCurrentUserName(), "Article", id);
                
                return Ok(new
                {
                    success = true,
                    data = article
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating article {ArticleId}", id);
                await _systemLogService.LogErrorAsync($"Failed to update article {id}", "Articles", "update", GetCurrentUserId(), GetCurrentUserName(), "Article", id, ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = "INTERNAL_ERROR",
                        message = "An error occurred while updating the article"
                    }
                });
            }
        }

        /// <summary>
        /// Delete an article
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteArticle(string id)
        {
            var userId = GetCurrentUserId();
            var deleted = await _articleService.DeleteArticleAsync(id, userId);
            
            if (!deleted)
                return NotFound(new { message = "Article not found" });

            await _systemLogService.LogSuccessAsync($"Article deleted: ID {id}", "Articles", "delete", GetCurrentUserId(), GetCurrentUserName(), "Article", id);
            
            return Ok(new { success = true, message = "Article deleted successfully", id });
        }

        // =====================================================
        // Inventory Transaction Endpoints
        // =====================================================

        /// <summary>
        /// Create a new inventory transaction
        /// </summary>
        [HttpPost("transactions")]
        public async Task<ActionResult<InventoryTransactionDto>> CreateTransaction([FromBody] CreateInventoryTransactionDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var transaction = await _articleService.CreateTransactionAsync(dto, userId);

                await _systemLogService.LogSuccessAsync($"Inventory transaction created for article {dto.ArticleId}: {dto.TransactionType} {dto.Quantity}", "Articles", "create", userId, GetCurrentUserName(), "InventoryTransaction", transaction.Id.ToString());
                
                return CreatedAtAction(nameof(GetArticleTransactions), new { articleId = dto.ArticleId }, new
                {
                    success = true,
                    data = transaction
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inventory transaction");
                await _systemLogService.LogErrorAsync("Failed to create inventory transaction", "Articles", "create", GetCurrentUserId(), GetCurrentUserName(), "InventoryTransaction", details: ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = "INTERNAL_ERROR",
                        message = "An error occurred while creating the transaction"
                    }
                });
            }
        }

        /// <summary>
        /// Get all transactions for a specific article
        /// </summary>
        [HttpGet("{articleId}/transactions")]
        public async Task<ActionResult> GetArticleTransactions(string articleId)
        {
            var transactions = await _articleService.GetArticleTransactionsAsync(articleId);
            
            return Ok(transactions);
        }

        /// <summary>
        /// Get recent inventory transactions across all articles.
        /// Used by the articles UI history feed and the offline hydration loop.
        /// </summary>
        [HttpGet("transactions")]
        public async Task<ActionResult> GetAllTransactions([FromQuery] int limit = 500)
        {
            var transactions = await _articleService.GetAllTransactionsAsync(limit);
            return Ok(new { success = true, data = transactions });
        }

        // =====================================================
        // Batch Operations
        // =====================================================

        /// <summary>
        /// Batch update stock levels
        /// </summary>
        [HttpPost("batch")]
        public async Task<ActionResult<BatchOperationResultDto>> BatchUpdateStock([FromBody] BatchUpdateStockDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _articleService.BatchUpdateStockAsync(dto, userId);

                await _systemLogService.LogSuccessAsync($"Batch stock update completed: {result.Updated} success, {result.Failed} failed", "Articles", "update", userId, GetCurrentUserName(), "Article");
                
                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch updating stock");
                await _systemLogService.LogErrorAsync("Batch stock update failed", "Articles", "update", GetCurrentUserId(), GetCurrentUserName(), "Article", details: ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = "INTERNAL_ERROR",
                        message = "An error occurred while batch updating stock"
                    }
                });
            }
        }

        // =====================================================
        // Category Endpoints
        // =====================================================

        /// <summary>
        /// Get all categories
        /// </summary>
        [HttpGet("categories")]
        public async Task<ActionResult> GetAllCategories()
        {
            var categories = await _articleService.GetAllCategoriesAsync();
            
            return Ok(categories);
        }

        /// <summary>
        /// Create a new category
        /// </summary>
        [HttpPost("categories")]
        public async Task<ActionResult<ArticleCategoryDto>> CreateCategory([FromBody] CreateArticleCategoryDto dto)
        {
            var category = await _articleService.CreateCategoryAsync(dto);

            await _systemLogService.LogSuccessAsync($"Article category created: {dto.Name}", "Articles", "create", GetCurrentUserId(), GetCurrentUserName(), "ArticleCategory", category.Id.ToString());
            
            return CreatedAtAction(nameof(GetAllCategories), new { id = category.Id }, category);
        }

        // =====================================================
        // Location Endpoints
        // =====================================================

        /// <summary>
        /// Get all locations
        /// </summary>
        [HttpGet("locations")]
        public async Task<ActionResult> GetAllLocations()
        {
            var locations = await _articleService.GetAllLocationsAsync();
            
            return Ok(locations);
        }

        /// <summary>
        /// Create a new location
        /// </summary>
        [HttpPost("locations")]
        public async Task<ActionResult<LocationDto>> CreateLocation([FromBody] CreateLocationDto dto)
        {
            var location = await _articleService.CreateLocationAsync(dto);

            await _systemLogService.LogSuccessAsync($"Location created: {dto.Name}", "Articles", "create", GetCurrentUserId(), GetCurrentUserName(), "Location", location.Id.ToString());
            
            return CreatedAtAction(nameof(GetAllLocations), new { id = location.Id }, location);
        }

        // =====================================================
        // Bulk Import Endpoint - Supports up to 10,000+ records
        // =====================================================

        /// <summary>
        /// Bulk import articles with batch processing for high performance.
        /// Supports up to 10,000+ records with automatic batching.
        /// </summary>
        [HttpPost("import")]
        public async Task<ActionResult<BulkImportArticleResultDto>> BulkImportArticles([FromBody] BulkImportArticleRequestDto importRequest)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userName = GetCurrentUserName();

                _logger.LogInformation("Starting bulk import of {Count} articles by user {UserId}", 
                    importRequest.Articles.Count, userId);

                var result = await _articleService.BulkImportArticlesAsync(importRequest, userId);

                await _systemLogService.LogSuccessAsync(
                    $"Bulk imported {result.SuccessCount} articles ({result.FailedCount} failures, {result.SkippedCount} skipped)", 
                    "Articles", "import", userId, userName, "Article");

                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk import of articles");
                await _systemLogService.LogErrorAsync("Bulk import failed", "Articles", "import", GetCurrentUserId(), GetCurrentUserName(), "Article", details: ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = "BULK_IMPORT_FAILED",
                        message = "An error occurred during bulk import",
                        details = ex.Message
                    }
                });
            }
        }
    }
}
