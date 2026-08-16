using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.PostSessions.Models;

public class OasPostSession : OasEntityBase
{
    public Guid ClientEventId { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public Guid? AssignmentId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? ShiftTemplateId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string StartedVia { get; set; } = "qr"; // qr|manual|biometric|pin
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
