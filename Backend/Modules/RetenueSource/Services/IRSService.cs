using MyApi.Modules.RetenueSource.DTOs;

namespace MyApi.Modules.RetenueSource.Services
{
    public interface IRSService
    {
        // ─── CRUD ───
        Task<PaginatedRSResponse> GetRSRecordsAsync(
            string? entityType = null,
            int? entityId = null,
            int? month = null,
            int? year = null,
            string? status = null,
            string? supplierTaxId = null,
            string? search = null,
            int page = 1,
            int limit = 20
        );

        Task<RSRecordDto?> GetRSRecordByIdAsync(int id);
        Task<RSRecordDto> CreateRSRecordAsync(CreateRSRecordDto dto, string userId);
        Task<RSRecordDto> UpdateRSRecordAsync(int id, UpdateRSRecordDto dto, string userId);
        Task<bool> DeleteRSRecordAsync(int id);

        // ─── Calculation ───
        RSCalculationDto CalculateRS(decimal amountPaid, string rsTypeCode);

        // ─── TEJ Export ───
        Task<TEJExportResponseDto> ExportTEJAsync(TEJExportRequestDto request, string userId);
        Task<List<TEJExportLogDto>> GetTEJExportLogsAsync(int? year = null);

        // Build the TEJ XML for a single supplier invoice on demand (download-any-time).
        // Returns missing-field messages when the invoice isn't ready, or the XML when it is.
        Task<TejInvoiceXmlResult> BuildTejXmlForSupplierInvoiceAsync(int supplierInvoiceId, string userId);

        // Build the TEJ XML for a purchase order (aggregates its RS-applicable invoices).
        Task<TejInvoiceXmlResult> BuildTejXmlForPurchaseOrderAsync(int purchaseOrderId, string userId);

        // ─── Stats ───
        Task<RSStatsDto> GetRSStatsAsync(string? entityType = null, int? entityId = null, int? month = null, int? year = null);
    }
}
