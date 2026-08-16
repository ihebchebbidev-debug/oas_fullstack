using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Cadences.DTOs;

public class OasCadenceDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid PostId { get; set; }
    public decimal Rate { get; set; }
    public int OperatorsRequired { get; set; }
    public int? ChangeoverTargetMin { get; set; }
}

public class OasCadenceRequestDto
{
    [Required] public Guid ProductId { get; set; }
    [Required] public Guid PostId { get; set; }
    [Required] public decimal Rate { get; set; }
    public int OperatorsRequired { get; set; } = 1;
    public int? ChangeoverTargetMin { get; set; }
}

public class OasCadenceVersionDto
{
    public int Version { get; set; }
    public decimal Rate { get; set; }
    public DateTimeOffset Since { get; set; }
}
