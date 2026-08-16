using MyApi.Modules.ExternalEndpoints.DTOs;

namespace MyApi.Modules.ExternalEndpoints.Services
{
    public interface IExternalEndpointService
    {
        Task<PaginatedEndpointResponse> GetEndpointsAsync(string? search = null, string? status = null, int page = 1, int limit = 20);
        Task<ExternalEndpointDto?> GetEndpointByIdAsync(int id);
        Task<ExternalEndpointDto> CreateEndpointAsync(CreateExternalEndpointDto dto, string userId);
        Task<ExternalEndpointDto> UpdateEndpointAsync(int id, UpdateExternalEndpointDto dto, string userId);
        Task<bool> DeleteEndpointAsync(int id, string userId);
        Task<ExternalEndpointDto> RegenerateKeyAsync(int id, string userId);
        Task<string> RevealKeyAsync(int id);
        Task<ExternalEndpointStatsDto> GetStatsAsync();

        // Logs
        Task<PaginatedLogResponse> GetLogsAsync(int endpointId, int page = 1, int limit = 20, string? companyId = null);
        Task<ExternalEndpointLogDto?> GetLogByIdAsync(int endpointId, int logId);
        Task<bool> DeleteLogAsync(int endpointId, int logId);
        Task<bool> ClearLogsAsync(int endpointId);
        Task<bool> MarkLogAsReadAsync(int endpointId, int logId);

        // Conversion — parse a received log payload and return suggested
        // offer/sale field values. Actual record creation happens via the
        // existing Offers/Sales modules with the user-confirmed data.
        Task<ConvertLogPreviewDto?> PreviewConvertLogAsync(int endpointId, int logId);

        // Public receive
        Task<(int statusCode, string responseBody)> ReceiveAsync(string slug, string method, string? headers, string? queryString, string? body, string? sourceIp, string? apiKey, string? originHeader = null, string? contentType = null, string? companyId = null);
    }
}
