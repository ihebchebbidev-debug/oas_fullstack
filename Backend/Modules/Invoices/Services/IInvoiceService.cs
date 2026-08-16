using System.Collections.Generic;
using System.Threading.Tasks;
using MyApi.Modules.Invoices.DTOs;

namespace MyApi.Modules.Invoices.Services
{
    public interface IInvoiceService
    {
        Task<PagedInvoiceResponse> GetInvoicesAsync(InvoiceQueryParams query);
        Task<InvoiceDto?> GetInvoiceByIdAsync(int id);

        // Explicit create (custom lines).
        Task<InvoiceDto> CreateDraftAsync(CreateInvoiceDto dto, string userId);

        // Snapshot every line of an existing Sale into a new draft invoice.
        // Used by ServiceOrderService.PrepareForInvoiceAsync so the invoice
        // reflects exactly what was billed.
        Task<InvoiceDto> CreateDraftFromSaleAsync(int saleId, string userId, int? serviceOrderId = null);

        Task<InvoiceDto> UpdateDraftAsync(int id, UpdateInvoiceDto dto, string userId);
        // `trigger` describes WHO/WHAT posted the invoice (e.g. "manual",
        // "auto:create_from_sale", "auto:payment_recording") and is written to the
        // structured logs plus the invoice/sale activity feeds for traceability.
        Task<InvoiceDto> PostAsync(int id, PostInvoiceDto dto, string userId, string? trigger = null);

        // Records why an automatic post attempt did NOT happen (or failed) on both the
        // invoice activity feed and the related sale/order activity timeline.
        Task LogAutoPostSkippedAsync(int invoiceId, string userId, string trigger, string reason);
        Task<InvoiceDto> VoidAsync(int id, VoidInvoiceDto dto, string userId);
        Task<InvoiceDto> MarkPaidAsync(int id, MarkPaidInvoiceDto dto, string userId);
        Task<InvoiceDto> ReopenAsync(int id, ReopenInvoiceDto dto, string userId);
        Task<bool> DeleteDraftAsync(int id, string userId);

        // Called by PaymentService whenever a payment against an invoice is
        // created/deleted so AmountPaid + Status stay in sync.
        Task RecalculatePaymentStateAsync(int invoiceId);

        // Audit-trail feed for the "Activity" tab on the invoice detail page.
        Task<IReadOnlyList<InvoiceActivityDto>> GetActivitiesAsync(int invoiceId);
    }
}
