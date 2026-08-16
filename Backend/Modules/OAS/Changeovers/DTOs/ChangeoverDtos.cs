using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Changeovers.DTOs;

public class OasChangeoverStepDto { public string Id { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public bool Done { get; set; } }

public class OasChangeoverDto
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid? FromProductId { get; set; }
    public Guid ToProductId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public List<OasChangeoverStepDto> Steps { get; set; } = new();
}

public class OasChangeoverRequestDto
{
    [Required] public Guid ClientEventId { get; set; }
    [Required] public Guid PostId { get; set; }
    public Guid? FromProductId { get; set; }
    [Required] public Guid ToProductId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public int? TargetMin { get; set; }
    /// <summary>Progress ticks — resubmitting the same ClientEventId with updated step states persists progress (spec v13: resumable after navigate-away).</summary>
    public List<OasChangeoverStepDto>? Steps { get; set; }
}
