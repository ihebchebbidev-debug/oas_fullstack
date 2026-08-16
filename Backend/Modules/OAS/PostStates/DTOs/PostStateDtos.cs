namespace MyApi.Modules.OAS.PostStates.DTOs;

public class OasPostStateDto
{
    public Guid PostId { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTimeOffset Since { get; set; }
    public Guid? ActiveEventId { get; set; }
    public Guid? CurrentUserId { get; set; }
    public Guid? CurrentProductId { get; set; }
}

public class OasPostStateHistoryDto
{
    public string State { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int? DurationSec { get; set; }
}
