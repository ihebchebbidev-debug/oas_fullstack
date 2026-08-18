using MyApi.Modules.OAS.ShopFloorAuth.DTOs;

namespace MyApi.Modules.OAS.ShopFloorAuth.Services;

public interface IOasOperatorService
{
    Task<IReadOnlyList<OasOperatorDto>> SearchAsync(int tenantId, string? q, Guid? scopeSiteId, Guid? scopeZoneId, Guid? scopeLineId);
    Task<(bool success, string? error, OasOperatorDto? operatorDto)> CreateAsync(int tenantId, OasCreateOperatorRequestDto request, string callerRole);
    Task<bool> SetActiveAsync(int tenantId, Guid id, bool isActive);
    Task<(bool success, string? error)> SetRoleAsync(int tenantId, Guid id, string role);
    Task<bool> SetScopeAsync(int tenantId, Guid id, OasSetScopeRequestDto request);
}
