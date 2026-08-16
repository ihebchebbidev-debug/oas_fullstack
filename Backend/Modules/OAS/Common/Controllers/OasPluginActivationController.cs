using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common.DTOs;
using MyApi.Modules.OAS.Common.Services;

namespace MyApi.Modules.OAS.Common.Controllers;

/// <summary>Spec §6.1 "Activation des plugins" — no [OasPluginGate] here (this endpoint IS the kill switch, it can't gate itself).</summary>
[Route("api/oas/plugin-activations")]
public class OasPluginActivationController : OasControllerBase
{
    private readonly IOasPluginActivationService _service;

    public OasPluginActivationController(IOasPluginActivationService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OasPluginActivationDto>>> GetAll()
    {
        var rows = await _service.GetAllAsync(CurrentTenantId);
        return Ok(rows.Select(r => new OasPluginActivationDto
        {
            PluginCode = r.PluginCode,
            Enabled = r.Enabled,
            IsCore = OasPluginRegistry.IsCore(r.PluginCode),
            UpdatedAt = r.UpdatedAt,
            UpdatedBy = r.UpdatedBy,
        }));
    }

    [HttpPut("{code}")]
    [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> SetEnabled(string code, [FromBody] OasSetPluginActivationRequestDto request)
    {
        var actor = User.FindFirst("oas_user_id")?.Value;
        var (success, error, cascaded) = await _service.SetEnabledAsync(CurrentTenantId, code, request.Enabled, actor);

        if (!success)
        {
            return error == "unknown_plugin"
                ? NotFound(new { error })
                : Problem(statusCode: 409, title: error ?? "conflict");
        }

        return Ok(new { success = true, cascaded });
    }

    [HttpPost("bulk")]
    [OasAuthorize(Roles = "admin")]
    public async Task<ActionResult<OasBulkPluginActivationResponseDto>> Reset()
    {
        var actor = User.FindFirst("oas_user_id")?.Value;
        var reset = await _service.BulkResetAsync(CurrentTenantId, actor);
        return Ok(new OasBulkPluginActivationResponseDto { Success = true, Reset = reset });
    }
}
