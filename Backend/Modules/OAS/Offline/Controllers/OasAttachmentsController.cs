using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Offline.DTOs;
using MyApi.Modules.OAS.Offline.Services;

namespace MyApi.Modules.OAS.Offline.Controllers;

[Route("api/oas/attachments")]
public class OasAttachmentsController : OasControllerBase
{
    private readonly IOasOfflineSyncService _service;
    public OasAttachmentsController(IOasOfflineSyncService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<OasAttachmentDto>> Create([FromBody] OasAttachmentRequestDto request)
        => Ok(await _service.CreateAttachmentAsync(CurrentTenantId, CurrentOasUserId, request));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OasAttachmentDto>>> GetAll([FromQuery] string entity, [FromQuery] Guid id)
        => Ok(await _service.GetAttachmentsAsync(CurrentTenantId, entity, id));
}
