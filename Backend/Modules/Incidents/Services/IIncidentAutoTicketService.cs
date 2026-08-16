using MyApi.Modules.SupportTickets.DTOs;

namespace MyApi.Modules.Incidents.Services
{
    public interface IIncidentAutoTicketService
    {
        Task<AutoIncidentResultDto> ProcessAsync(AutoIncidentReportDto dto, string tenant);
    }
}
