using MyApi.Modules.Dashboards.DTOs;

namespace MyApi.Modules.Dashboards.Services;

public interface IDashboardLayoutService
{
    Task<DashboardLayoutDto> GetAsync(int userId, string scope, CancellationToken ct = default);
    Task<DashboardLayoutDto> SaveAsync(int userId, SaveDashboardLayoutRequest req, CancellationToken ct = default);
    Task<bool> ResetAsync(int userId, string scope, CancellationToken ct = default);
}
