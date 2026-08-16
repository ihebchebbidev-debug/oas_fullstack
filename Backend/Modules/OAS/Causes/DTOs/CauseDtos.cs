using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Causes.DTOs;

public class OasCauseDto
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string LabelFr { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public string? EventType { get; set; }
    public string DefaultCriticality { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<OasCauseDto> Children { get; set; } = new();
}

public class OasCauseRequestDto
{
    public Guid? ParentId { get; set; }
    [Required] public string Domain { get; set; } = string.Empty;
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string LabelFr { get; set; } = string.Empty;
    [Required] public string LabelAr { get; set; } = string.Empty;
    public string? EventType { get; set; }
    public string DefaultCriticality { get; set; } = "medium";
}

public class OasCauseKindRequestDto { [Required] public string EventType { get; set; } = string.Empty; }
public class OasCauseCriticalityRequestDto { [Required] public string Criticality { get; set; } = string.Empty; }
public class OasCauseActiveRequestDto { public bool IsActive { get; set; } }
public class OasCauseUsageDto { public Guid CauseId { get; set; } public int Count { get; set; } }

public class OasCauseProposalDto
{
    public Guid Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string LabelFr { get; set; } = string.Empty;
    public string? LabelAr { get; set; }
    public Guid ProposedBy { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
public class OasCauseProposalRequestDto
{
    [Required] public string Domain { get; set; } = string.Empty;
    [Required] public string LabelFr { get; set; } = string.Empty;
    public string? LabelAr { get; set; }
}
public class OasCauseProposalReviewRequestDto
{
    [Required] public string Decision { get; set; } = string.Empty; // "accept" | "reject"
    public string? Code { get; set; } // required if accept — becomes the new cause's code
    public Guid? ParentId { get; set; }
}
