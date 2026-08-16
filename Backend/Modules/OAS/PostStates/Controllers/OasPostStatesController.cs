using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.PostStates.DTOs;
using MyApi.Modules.OAS.PostStates.Services;

namespace MyApi.Modules.OAS.PostStates.Controllers;

[Route("api/oas/post-states")]
[OasPluginGate("OA0003SHOPFLOOR")]
public class OasPostStatesController : OasControllerBase
{
    private readonly IOasPostStateService _service;
    public OasPostStatesController(IOasPostStateService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasPostStateDto>>> Live() => Ok(await _service.GetLiveAsync(CurrentTenantId));

    [HttpGet("{postId}/history")]
    public async Task<ActionResult<IReadOnlyList<OasPostStateHistoryDto>>> History(Guid postId) => Ok(await _service.GetHistoryAsync(CurrentTenantId, postId));
}
