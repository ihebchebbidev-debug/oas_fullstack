using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Interventions.DTOs;

public class OasInterventionDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid? AssigneeId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class OasCreateInterventionRequestDto { [Required] public Guid EventId { get; set; } }
