using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Articles.DTOs
{
    public class StockTransactionDto
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string? ArticleName { get; set; }
        public string? ArticleNumber { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal PreviousStock { get; set; }
        public decimal NewStock { get; set; }
        public string? Reason { get; set; }
        public string? ReferenceType { get; set; }
        public string? ReferenceId { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public string PerformedBy { get; set; } = string.Empty;
        public string? PerformedByName { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateStockTransactionDto
    {
        [Required]
        public int ArticleId { get; set; }

        [Required]
        [MaxLength(50)]
        public string TransactionType { get; set; } = string.Empty; // add, remove, adjustment, sale_deduction, return, etc.

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than zero")]
        public decimal Quantity { get; set; }

        [MaxLength(255)]
        public string? Reason { get; set; }

        [MaxLength(50)]
        public string? ReferenceType { get; set; } // manual, sale, offer

        [MaxLength(50)]
        public string? ReferenceId { get; set; }

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        public string? Notes { get; set; }
    }

    public class StockTransactionListDto
    {
        public List<StockTransactionDto> Data { get; set; } = new();
        public PaginationDto Pagination { get; set; } = new();
    }

    public class StockTransactionSearchDto
    {
        public int? ArticleId { get; set; }
        public string? TransactionType { get; set; }
        public string? ReferenceType { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? PerformedBy { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 50;
        public string SortBy { get; set; } = "created_at";
        public string SortOrder { get; set; } = "desc";
    }

    public class DeductStockFromSaleDto
    {
        [Required]
        public int SaleId { get; set; }
        
        public string? UserId { get; set; }
        public string? UserName { get; set; }
    }

    public class StockDeductionResultDto
    {
        public bool Success { get; set; }
        public int ItemsProcessed { get; set; }
        public int ItemsDeducted { get; set; }
        public int ItemsRestored { get; set; }  // Added for restore operations
        public int ItemsFailed { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<StockTransactionDto> Transactions { get; set; } = new();
    }
}
