using MyApi.Modules.Reporting.DTOs;

namespace MyApi.Modules.Reporting.Services;

public interface IReportingFavoritesService
{
    Task<ReportingFavoritesResponse> GetAsync(int userId, string scope, CancellationToken ct = default);
    Task<ReportingFavoriteDto> UpsertAsync(int userId, UpsertReportingFavoriteRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(int userId, string scope, string widgetId, CancellationToken ct = default);
    Task<int> DeleteAllAsync(int userId, string scope, CancellationToken ct = default);
    Task ReorderAsync(int userId, ReorderReportingFavoritesRequest req, CancellationToken ct = default);
}