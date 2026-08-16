namespace MyApi.Modules.OAS.Audit.DTOs;

public class OasAuditLogDto
{
    public Guid Id { get; set; }
    public string EntityTable { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
