using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Products.DTOs;
using MyApi.Modules.OAS.Products.Services;

namespace MyApi.Modules.OAS.Products.Controllers;

[Route("api/oas/products")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasProductsController : OasControllerBase
{
    private readonly IOasProductService _service;
    public OasProductsController(IOasProductService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasProductDto>>> GetAll() => Ok(await _service.GetProductsAsync(CurrentTenantId));

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasProductDto>> Create([FromBody] OasProductRequestDto request) => Ok(await _service.CreateProductAsync(CurrentTenantId, request));

    [HttpPut("{id}")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OasProductRequestDto request)
        => await _service.UpdateProductAsync(CurrentTenantId, id, request) ? Ok(new { success = true }) : NotFound();

    [HttpDelete("{id}")] [OasAuthorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteProductAsync(CurrentTenantId, id) ? Ok(new { success = true }) : NotFound();
}

[Route("api/oas/production-orders")]
[OasPluginGate("OA0008REFERENTIALS")]
public class OasProductionOrdersController : OasControllerBase
{
    private readonly IOasProductService _service;
    public OasProductionOrdersController(IOasProductService service) => _service = service;

    [HttpGet] public async Task<ActionResult<IReadOnlyList<OasProductionOrderDto>>> GetAll() => Ok(await _service.GetOrdersAsync(CurrentTenantId));

    [HttpPost] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<ActionResult<OasProductionOrderDto>> Create([FromBody] OasProductionOrderRequestDto request) => Ok(await _service.CreateOrderAsync(CurrentTenantId, request));

    [HttpPut("{id}/status")] [OasAuthorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] OasProductionOrderStatusRequestDto request)
    {
        var (success, error) = await _service.SetOrderStatusAsync(CurrentTenantId, id, request.Status);
        if (!success) return error == "not_found" ? NotFound() : BadRequest(new { error });
        return Ok(new { success = true });
    }
}
