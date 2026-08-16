using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Equipments.DTOs;

public class OasEquipmentDto
{
    public Guid Id { get; set; }
    public Guid? PostId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public DateOnly? CommissionedAt { get; set; }
    public string Criticality { get; set; } = string.Empty;
}

public class OasEquipmentRequestDto
{
    public Guid? PostId { get; set; }
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public DateOnly? CommissionedAt { get; set; }
    public string Criticality { get; set; } = "medium";
}
