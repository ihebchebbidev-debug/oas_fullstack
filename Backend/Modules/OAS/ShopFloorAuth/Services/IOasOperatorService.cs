using MyApi.Modules.OAS.ShopFloorAuth.DTOs;

namespace MyApi.Modules.OAS.ShopFloorAuth.Services;

public interface IOasOperatorService
{
    Task<IReadOnlyList<OasOperatorDto>> SearchAsync(int tenantId, string? q, Guid? scopeSiteId, Guid? scopeZoneId, Guid? scopeLineId);
    Task<(bool success, string? error, OasOperatorDto? operatorDto)> CreateAsync(int tenantId, OasCreateOperatorRequestDto request, string callerRole);
    Task<(bool success, string? error)> SetActiveAsync(int tenantId, Guid id, bool isActive, string callerRole);
    Task<(bool success, string? error)> SetRoleAsync(int tenantId, Guid id, string role, string callerRole);
    Task<bool> SetScopeAsync(int tenantId, Guid id, OasSetScopeRequestDto request);
}
