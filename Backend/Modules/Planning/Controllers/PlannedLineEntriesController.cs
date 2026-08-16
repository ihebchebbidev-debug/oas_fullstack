using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Planning.DTOs;
using MyApi.Modules.Planning.Services;

namespace MyApi.Modules.Planning.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/planned-entries")]
    public class PlannedLineEntriesController : ControllerBase
    {
        private readonly IPlannedLineEntryService _svc;
        public PlannedLineEntriesController(IPlannedLineEntryService svc) { _svc = svc; }

        private string UserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? "system";

        /// <summary>List planned entries for a given parent (offer_item|sale_item|service_order_job).</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string parentType, [FromQuery] int parentId)
            => Ok(await _svc.GetForParentAsync(parentType, parentId));

        [HttpPost]
        public async Task<IActionResult> Create([FromQuery] string parentType, [FromQuery] int parentId, [FromBody] CreatePlannedLineEntryDto dto)
            => Ok(await _svc.CreateAsync(parentType, parentId, dto, UserId));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePlannedLineEntryDto dto)
            => Ok(await _svc.UpdateAsync(id, dto, UserId));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var syncWarning = await _svc.DeleteAsync(id, UserId);
            // Always return 200 with a body so the UI can surface a planning-sync
            // failure toast; syncWarning is null on the happy path.
            return Ok(new { syncWarning });
        }

        /// <summary>Plan vs actual rollup for a given service order job.</summary>
        [HttpGet("plan-vs-actual/{serviceOrderJobId:int}")]
        public async Task<IActionResult> PlanVsActual(int serviceOrderJobId)
            => Ok(await _svc.GetPlanVsActualAsync(serviceOrderJobId));

        /// <summary>Copy all planned entries from one parent to another.</summary>
        [HttpPost("copy")]
        public async Task<IActionResult> Copy(
            [FromQuery] string sourceParentType,
            [FromQuery] int sourceParentId,
            [FromQuery] string targetParentType,
            [FromQuery] int targetParentId)
        {
            await _svc.CopyAsync(sourceParentType, sourceParentId, targetParentType, targetParentId, UserId);
            return NoContent();
        }
    }
}
