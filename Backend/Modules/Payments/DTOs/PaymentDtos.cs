using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Payments.DTOs
{
    // ── Payment DTOs ──────────────────────────────────
    public class PaymentDto
    {
        public string Id { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string? PlanId { get; set; }
        public string? InstallmentId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TND";
        public string PaymentMethod { get; set; } = "cash";
        public string? PaymentReference { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Status { get; set; } = "completed";
        public string? Notes { get; set; }
        public string? ReceiptNumber { get; set; }
        // Legacy single-proof fields, kept so older clients keep working.
        // Mirrors the first entry of ProofDocuments.
        public int? ProofDocumentId { get; set; }
        public string? ProofDocumentName { get; set; }
        public string? ProofDocumentUrl { get; set; }
        public List<PaymentProofDocumentDto> ProofDocuments { get; set; } = new();
        public List<PaymentItemAllocationDto> ItemAllocations { get; set; } = new();
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreatePaymentDto
    {
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Amount must be greater than zero")]
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TND";
        public string PaymentMethod { get; set; } = "cash";
        public string? PaymentReference { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? Notes { get; set; }
        public string? InstallmentId { get; set; }
        // Optional proof-of-payment attachments (already uploaded via /api/Documents).
        // Single-file fields kept for backward compatibility; ProofDocuments is preferred.
        public int? ProofDocumentId { get; set; }
        public string? ProofDocumentName { get; set; }
        public string? ProofDocumentUrl { get; set; }
        public List<CreatePaymentProofDocumentDto>? ProofDocuments { get; set; }
        public List<CreatePaymentItemAllocationDto>? ItemAllocations { get; set; }
    }

    public class PaymentProofDocumentDto
    {
        public string Id { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public int? DocumentId { get; set; }
        public string? DocumentName { get; set; }
        public string? DocumentUrl { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreatePaymentProofDocumentDto
    {
        public int? DocumentId { get; set; }
        public string? DocumentName { get; set; }
        public string? DocumentUrl { get; set; }
    }

    public class UpdatePaymentProofDocumentDto
    {
        [Required]
        [MaxLength(500)]
        public string DocumentName { get; set; } = string.Empty;
    }

    public class PaymentItemAllocationDto
    {
        public string Id { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal AllocatedAmount { get; set; }
        public decimal ItemTotal { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreatePaymentItemAllocationDto
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Allocated amount must be greater than zero")]
        public decimal AllocatedAmount { get; set; }
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Item total must be greater than zero")]
        public decimal ItemTotal { get; set; }
    }

    // ── Payment Plan DTOs ─────────────────────────────
    public class PaymentPlanDto
    {
        public string Id { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "TND";
        public int InstallmentCount { get; set; }
        public string Status { get; set; } = "active";
        public List<PaymentPlanInstallmentDto> Installments { get; set; } = new();
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreatePaymentPlanDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "TND";
        public List<CreateInstallmentDto> Installments { get; set; } = new();
    }

    public class PaymentPlanInstallmentDto
    {
        public string Id { get; set; } = string.Empty;
        public string PlanId { get; set; } = string.Empty;
        public int InstallmentNumber { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "pending";
        public decimal PaidAmount { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateInstallmentDto
    {
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Installment amount must be greater than zero")]
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }

    // ── Summary & Statement DTOs ──────────────────────
    public class PaymentSummaryDto
    {
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string PaymentStatus { get; set; } = "unpaid";
        public int PaymentCount { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public string Currency { get; set; } = "TND";
    }

    public class PaymentStatementDto
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string EntityTitle { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string Currency { get; set; } = "TND";
        public List<PaymentDto> Payments { get; set; } = new();
        public PaymentPlanDto? Plan { get; set; }
        public List<ItemPaymentBreakdownDto> Items { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class ItemPaymentBreakdownDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
    }
}
