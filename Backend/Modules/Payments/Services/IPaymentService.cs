using MyApi.Modules.Payments.DTOs;

namespace MyApi.Modules.Payments.Services
{
    public interface IPaymentService
    {
        // Payments CRUD
        Task<List<PaymentDto>> GetPaymentsAsync(string entityType, string entityId);
        Task<PaymentDto> CreatePaymentAsync(string entityType, string entityId, CreatePaymentDto dto, string userId, string userName);
        Task<bool> DeletePaymentAsync(string entityType, string entityId, string paymentId);

        // Proof-of-payment documents (multiple per payment)
        Task<List<PaymentProofDocumentDto>> GetPaymentProofsAsync(string entityType, string entityId, string paymentId);
        Task<List<PaymentProofDocumentDto>> AddPaymentProofsAsync(string entityType, string entityId, string paymentId, List<CreatePaymentProofDocumentDto> proofs, string userId);
        Task<PaymentProofDocumentDto?> UpdatePaymentProofAsync(string entityType, string entityId, string paymentId, string proofId, string documentName);
        Task<bool> DeletePaymentProofAsync(string entityType, string entityId, string paymentId, string proofId);

        // Payment Summary
        Task<PaymentSummaryDto> GetPaymentSummaryAsync(string entityType, string entityId);

        // Payment Plans
        Task<List<PaymentPlanDto>> GetPaymentPlansAsync(string entityType, string entityId);
        Task<PaymentPlanDto> CreatePaymentPlanAsync(string entityType, string entityId, CreatePaymentPlanDto dto, string userId);
        Task<bool> DeletePaymentPlanAsync(string entityType, string entityId, string planId);

        // Statement
        Task<PaymentStatementDto> GetPaymentStatementAsync(string entityType, string entityId);

        // Reminder check (used by background service)
        Task<List<InstallmentReminderInfo>> GetUpcomingInstallmentsAsync(int daysAhead = 3);
    }

    public class InstallmentReminderInfo
    {
        public string PlanId { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string InstallmentId { get; set; } = string.Empty;
        public int InstallmentNumber { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TND";
        public DateTime DueDate { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        // Populated from parent entity
        public string? EntityTitle { get; set; }
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public int? ContactId { get; set; }
    }
}
