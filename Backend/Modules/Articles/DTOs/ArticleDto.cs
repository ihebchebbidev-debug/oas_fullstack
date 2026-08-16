using System;
using System.Collections.Generic;

namespace MyApi.Modules.Articles.DTOs
{
    // =====================================================
    // Article DTOs - Aligned with Database Schema
    // =====================================================

    public class ArticleDto
    {
        public int Id { get; set; }
        public string ArticleNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public string Unit { get; set; } = "piece";
        public decimal PurchasePrice { get; set; }
        public decimal SalesPrice { get; set; }
        public decimal StockQuantity { get; set; }
        public decimal? MinStockLevel { get; set; }
        public int? LocationId { get; set; }
        public int? GroupId { get; set; }
        public string? Supplier { get; set; }
        public string Type { get; set; } = "material";
        public int? Duration { get; set; }  // Duration in minutes for services
        public string[]? SkillsRequired { get; set; }  // Required skills for this service article
        public decimal TvaRate { get; set; } = 19.00m;  // TVA/VAT rate percentage
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class CreateArticleDto
    {
        public string? ArticleNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public string Unit { get; set; } = "piece";
        public decimal PurchasePrice { get; set; }
        public decimal SalesPrice { get; set; }
        public decimal StockQuantity { get; set; }
        public decimal? MinStockLevel { get; set; }
        public int? LocationId { get; set; }
        public int? GroupId { get; set; }
        public string? Supplier { get; set; }
        public string Type { get; set; } = "material";
        public int? Duration { get; set; }  // Duration in minutes for services
        public string[]? SkillsRequired { get; set; }  // Required skill names for this service
        public decimal TvaRate { get; set; } = 19.00m;  // TVA/VAT rate percentage
        public bool IsActive { get; set; } = true;
    }

    public class UpdateArticleDto
    {
        public string? ArticleNumber { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public string? Unit { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? SalesPrice { get; set; }
        public decimal? StockQuantity { get; set; }
        public decimal? MinStockLevel { get; set; }
        public int? GroupId { get; set; }
        public int? LocationId { get; set; }
        public string? Supplier { get; set; }
        public string? Type { get; set; }
        public int? Duration { get; set; }  // Duration in minutes for services
        public string[]? SkillsRequired { get; set; }  // Required skill names (null = no change)
        public decimal? TvaRate { get; set; }  // TVA/VAT rate percentage
        public bool? IsActive { get; set; }
    }

    public class ArticleListDto
    {
        public List<ArticleDto> Data { get; set; } = new();
        public PaginationDto Pagination { get; set; } = new();
    }

    public class PaginationDto
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Pages { get; set; }
    }

    // =====================================================
    // Category DTOs
    // =====================================================

    public class ArticleCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentCategoryId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateArticleCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentCategoryId { get; set; }
    }

    // =====================================================
    // Location DTOs
    // =====================================================

    public class LocationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateLocationDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // =====================================================
    // Inventory Transaction DTOs
    // =====================================================

    public class InventoryTransactionDto
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class CreateInventoryTransactionDto
    {
        public int ArticleId { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }

    // =====================================================
    // Batch Operation DTOs
    // =====================================================

    public class BatchUpdateStockDto
    {
        public List<StockUpdateItem> Items { get; set; } = new();
    }

    public class StockUpdateItem
    {
        public int Id { get; set; }
        public decimal StockQuantity { get; set; }
    }

    public class BatchOperationResultDto
    {
        public bool Success { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
