using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Infrastructure;
using MyApi.Modules.Purchases.DTOs;
using MyApi.Modules.Purchases.Services;
using MyApi.Modules.Shared.Services;
using System.Security.Claims;

namespace MyApi.Modules.Purchases.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/article-suppliers")]
    public class ArticleSuppliersController : ControllerBase
    {
        private readonly IArticleSupplierService _service;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<ArticleSuppliersController> _logger;

        public ArticleSuppliersController(IArticleSupplierService service, ISystemLogService systemLogService, ILogger<ArticleSuppliersController> logger)
        {
            _service = service;
            _systemLogService = systemLogService;
            _logger = logger;
        }

        private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        private string GetUserName() => User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value ?? "anonymous";

        [RequirePermission("purchases", "read")]
        [HttpGet("by-article/{articleId:int}")]
        public async Task<IActionResult> GetByArticle(int articleId)
        {
            try
            {
                var result = await _service.GetByArticleAsync(articleId);
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching suppliers for article {ArticleId}", articleId);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "read")]
        [HttpGet("by-supplier/{supplierId:int}")]
        public async Task<IActionResult> GetBySupplier(int supplierId)
        {
            try
            {
                var result = await _service.GetBySupplierAsync(supplierId);
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching articles for supplier {SupplierId}", supplierId);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "read")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var result = await _service.GetByIdAsync(id);
                if (result == null) return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Article-supplier relationship not found" } });
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching article-supplier {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "create")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateArticleSupplierDto dto)
        {
            try
            {
                var userId = GetUserId();
                var result = await _service.CreateAsync(dto, userId);
                await _systemLogService.LogSuccessAsync($"Article-supplier created: Article {dto.ArticleId} - Supplier {dto.SupplierId}", "Purchases", "create", userId, GetUserName(), "ArticleSupplier", result.Id.ToString());
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, new { success = true, data = result });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating article-supplier");
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "update")]
        [HttpPatch("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateArticleSupplierDto dto)
        {
            try
            {
                var userId = GetUserId();
                var result = await _service.UpdateAsync(id, dto, userId);
                await _systemLogService.LogSuccessAsync($"Article-supplier updated: {id}", "Purchases", "update", userId, GetUserName(), "ArticleSupplier", id.ToString());
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating article-supplier {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "delete")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (!await _service.DeleteAsync(id, GetUserId()))
                    return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Not found" } });
                await _systemLogService.LogSuccessAsync($"Article-supplier deleted: {id}", "Purchases", "delete", GetUserId(), GetUserName(), "ArticleSupplier", id.ToString());
                return Ok(new { success = true, message = "Deleted successfully" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting article-supplier {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }

        [RequirePermission("purchases", "read")]
        [HttpGet("{id:int}/price-history")]
        public async Task<IActionResult> GetPriceHistory(int id)
        {
            try
            {
                var result = await _service.GetPriceHistoryAsync(id);
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = ex.Message } }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching price history for article-supplier {Id}", id);
                return StatusCode(500, new { success = false, error = new { code = "INTERNAL_ERROR", message = "Une erreur interne est survenue." } });
            }
        }
    }
}
