using MyApi.Modules.OAS.ShopFloorAuth.DTOs;

namespace MyApi.Modules.OAS.ShopFloorAuth.Services;

public interface IOasShopFloorAuthService
{
    Task<OasAuthResponseDto> LoginAsync(string oasSlug, int tenantId, OasShopFloorLoginRequestDto request);
    Task<OasPinRegenerateResponseDto> RegeneratePinAsync(int tenantId, Guid targetUserId, string actorRole);
}
