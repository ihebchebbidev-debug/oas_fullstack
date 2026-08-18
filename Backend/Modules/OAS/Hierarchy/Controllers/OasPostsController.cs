using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Hierarchy.DTOs;
using MyApi.Modules.OAS.Hierarchy.Services;

namespace MyApi.Modules.OAS.Hierarchy.Controllers;

[Route("api/oas/posts")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasPostsController : OasControllerBase
{
    private readonly IOasHierarchyService _service;
    public OasPostsController(IOasHierarchyService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasPostDto>>> GetAll([FromQuery] Guid? lineId) => Ok(await _service.GetPostsAsync(CurrentTenantId, lineId));

    [HttpGet("{id}")]
    public async Task<ActionResult<OasPostDto>> GetOne(Guid id)
    {
        var post = await _service.GetPostAsync(CurrentTenantId, id);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<OasPostDto>> GetByCode(string code)
    {
        var post = await _service.GetPostByCodeAsync(CurrentTenantId, code);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<ActionResult<OasPostDto>> Create([FromBody] OasPostRequestDto request) => Ok(await _service.CreatePostAsync(CurrentTenantId, request));

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasPostRequestDto request)
        => await _service.UpdatePostAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpPut("{id}/attributes")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> UpdateAttributes(Guid id, [FromBody] OasPostAttributesRequestDto request)
        => await _service.UpdatePostAttributesAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpPut("{id}/critical")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> SetCritical(Guid id, [FromBody] OasPostCriticalRequestDto request)
        => await _service.SetPostCriticalAsync(CurrentTenantId, id, request.IsCritical) ? Ok(new { success = true }) : NotFound();

    [HttpPost("{id}/archive")] [OasAuthorize(Roles = "admin")] [OasWorkspace("web")]
    public async Task<IActionResult> Archive(Guid id)
        => await _service.ArchivePostAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();

    [HttpGet("{id}/capacity")]
    public async Task<ActionResult<int>> Capacity(Guid id) => Ok(new { operatorsRequired = await _service.GetPostCapacityAsync(CurrentTenantId, id) });

    // Verified: qr-token(s)/rotate are only called from Admin.tsx's "qr" tab (web).
    [HttpGet("{id}/qr-token")] [OasWorkspace("web")]
    public async Task<ActionResult<OasQrTokenDto>> GetQrToken(Guid id)
    {
        var token = await _service.GetPostQrTokenAsync(CurrentTenantId, id);
        return token is null ? NotFound() : Ok(token);
    }

    [HttpPost("{id}/qr-token/rotate")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<ActionResult<OasQrTokenDto>> RotateQrToken(Guid id)
    {
        var token = await _service.RotatePostQrTokenAsync(CurrentTenantId, id);
        return token is null ? NotFound() : Ok(token);
    }

    [HttpGet("qr-tokens")] [OasWorkspace("web")]
    public async Task<ActionResult<IReadOnlyList<OasQrTokenDto>>> GetQrTokens([FromQuery] Guid? lineId) => Ok(await _service.GetQrTokensForLineAsync(CurrentTenantId, lineId));

    [HttpGet("layout")]
    public async Task<ActionResult<IReadOnlyList<OasPostLayoutDto>>> GetLayout([FromQuery] Guid? lineId) => Ok(await _service.GetLayoutAsync(CurrentTenantId, lineId));

    [HttpPut("layout")] [OasAuthorize(Roles = "admin,supervisor")] [OasWorkspace("web")]
    public async Task<IActionResult> PutLayout([FromBody] List<OasPostLayoutDto> entries)
    {
        await _service.PutLayoutAsync(CurrentTenantId, entries);
        return Ok(new { success = true });
    }
}

[Route("api/oas/hierarchy")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasHierarchyTreeController : OasControllerBase
{
    private readonly IOasHierarchyService _service;
    public OasHierarchyTreeController(IOasHierarchyService service) => _service = service;

    [HttpGet("tree")]
    public async Task<ActionResult<IReadOnlyList<OasHierarchyTreeSiteDto>>> Tree() => Ok(await _service.GetTreeAsync(CurrentTenantId));
}

[Route("api/oas/referentials")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasReferentialsController : OasControllerBase
{
    private readonly IOasHierarchyService _service;
    public OasReferentialsController(IOasHierarchyService service) => _service = service;

    // Verified: only Referentials.tsx (web) calls this.
    [HttpGet("completeness")] [OasWorkspace("web")]
    public async Task<ActionResult<OasCompletenessDto>> Completeness() => Ok(await _service.GetCompletenessAsync(CurrentTenantId));
}
