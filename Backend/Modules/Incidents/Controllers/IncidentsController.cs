using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Infrastructure;
using MyApi.Modules.Incidents.Services;
using MyApi.Modules.SupportTickets.DTOs;

namespace MyApi.Modules.Incidents.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IncidentsController : ControllerBase
    {
        private readonly IIncidentAutoTicketService _incidentService;
        private readonly ILogger<IncidentsController> _logger;

        public IncidentsController(
            IIncidentAutoTicketService incidentService,
            ILogger<IncidentsController> logger)
        {
            _incidentService = incidentService;
            _logger = logger;
        }

        // Resolve the tenant the same way the DbContext registration does:
        // middleware-resolved value first, header fallback, then the default
        // shared DB (empty) — never the bogus "unknown" slug.
        private string GetTenant() => TenantResolution.Resolve(HttpContext);

        /// <summary>
        /// POST /api/Incidents/auto — Evaluate an incident and create or update a support ticket.
        /// </summary>
        [HttpPost("auto")]
        [AllowAnonymous]
        public async Task<ActionResult<AutoIncidentResultDto>> ReportAuto([FromBody] AutoIncidentReportDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.IncidentType))
                return BadRequest(new { error = "IncidentType is required" });
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { error = "Message is required" });

            try
            {
                var tenant = GetTenant();
                var result = await _incidentService.ProcessAsync(dto, tenant);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process auto incident report");
                return StatusCode(500, new { error = "Failed to process incident report" });
            }
        }
    }
}
