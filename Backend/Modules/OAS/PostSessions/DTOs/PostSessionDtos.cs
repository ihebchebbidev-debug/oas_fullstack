using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.PostSessions.DTOs;

public class OasPostSessionDto
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public Guid? AssignmentId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ShiftTemplateId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}

public class OasOpenSessionRequestDto
{
    [Required] public Guid ClientEventId { get; set; }
    [Required] public Guid PostId { get; set; }
    public Guid? AssignmentId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ShiftTemplateId { get; set; }
    public string StartedVia { get; set; } = "qr";
    /// <summary>v15: if a different active session already blocks this open, the caller must pass true to explicitly force-relay it — silent overwrite is exactly the bug being fixed.</summary>
    public bool ForceRelay { get; set; }
}

public class OasRelaySessionRequestDto
{
    [Required] public Guid ClientEventId { get; set; }
    [Required] public Guid NewUserId { get; set; }
}

public class OasCloseSessionRequestDto
{
    [Required] public Guid ClientEventId { get; set; }
}

public class OasScanRequestDto { [Required] public string Code { get; set; } = string.Empty; }
public class OasScanResultDto { public Guid? PostId { get; set; } public string? PostCode { get; set; } public bool Resolved { get; set; } }
