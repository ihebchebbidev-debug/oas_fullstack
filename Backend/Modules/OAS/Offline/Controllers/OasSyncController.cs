using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Offline.DTOs;
using MyApi.Modules.OAS.Offline.Services;

namespace MyApi.Modules.OAS.Offline.Controllers;

[Route("api/oas/sync")]
public class OasSyncController : OasControllerBase
{
    private readonly IOasOfflineSyncService _service;
    public OasSyncController(IOasOfflineSyncService service) => _service = service;

    [HttpPost("push")]
    public async Task<ActionResult<OasSyncPushResponseDto>> Push([FromBody] OasSyncPushRequestDto request) => Ok(await _service.PushAsync(CurrentTenantId, request));

    [HttpGet("pull")]
    public async Task<ActionResult<OasSyncPullResponseDto>> Pull([FromQuery] DateTimeOffset since, [FromQuery] int limit = 200)
        => Ok(await _service.PullAsync(CurrentTenantId, since, Math.Clamp(limit, 1, 500)));

    [HttpGet("receipts")]
    public async Task<ActionResult<IReadOnlyList<OasSyncReceiptDto>>> Receipts() => Ok(await _service.GetReceiptsAsync(CurrentTenantId));
}
