using MyApi.Modules.OfflineHydration.DTOs;

namespace MyApi.Modules.OfflineHydration.Services;

public interface IOfflineHydrationPreferencesService
{
    Task<OfflineHydrationModulesDto> GetForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<OfflineHydrationModulesDto> UpsertForUserAsync(int userId, Dictionary<string, bool> modules, CancellationToken cancellationToken = default);
}
