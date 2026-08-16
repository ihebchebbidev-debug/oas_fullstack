using System;
using System.Collections.Generic;

namespace MyApi.Modules.Invoices.DTOs
{
    public class InvoiceActivityDto
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class InvoiceDto
    {
        public int Id { get; set; }
        public string? InvoiceNumber { get; set; }
        public string Status { get; set; } = "draft";
        public int ContactId { get; set; }
        public string? ContactName { get; set; }
        public int? SaleId { get; set; }
        public string? SaleNumber { get; set; }
        public int? ServiceOrderId { get; set; }
        public string? Title { get; set; }
        public string? Notes { get; set; }
        public string Currency { get; set; } = "TND";
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal AmountDue => Math.Max(0m, GrandTotal - AmountPaid);
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? PostedAt { get; set; }
        public DateTime? VoidedAt { get; set; }
        public string? VoidReason { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<InvoiceLineDto> Lines { get; set; } = new();
    }

    public class InvoiceLineDto
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public string? SourceType { get; set; }
        public string? SourceId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; }
        public decimal LineTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class CreateInvoiceDto
    {
        public int ContactId { get; set; }
        public int? SaleId { get; set; }
        public int? ServiceOrderId { get; set; }
        public string? Title { get; set; }
        public string? Notes { get; set; }
        public string? Currency { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public List<CreateInvoiceLineDto> Lines { get; set; } = new();
    }

    public class CreateInvoiceLineDto
    {
        public string? SourceType { get; set; }
        public string? SourceId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Quantity { get; set; } = 1;
        public string? Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class UpdateInvoiceDto
    {
        public string? Title { get; set; }
        public string? Notes { get; set; }
        public string? Currency { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public List<CreateInvoiceLineDto>? Lines { get; set; }
    }

    public class PostInvoiceDto
    {
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class VoidInvoiceDto
    {
        public string? Reason { get; set; }
    }

    public class MarkPaidInvoiceDto
    {
        public string? Memo { get; set; }
    }

    public class ReopenInvoiceDto
    {
        public string? Memo { get; set; }
    }

    public class InvoiceQueryParams
    {
        public string? Status { get; set; }
        public int? ContactId { get; set; }
        public int? SaleId { get; set; }
        public int? ServiceOrderId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
        public string SortBy { get; set; } = "created_at";
        public string SortOrder { get; set; } = "desc";
    }

    public class PagedInvoiceResponse
    {
        public List<InvoiceDto> Data { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}
