namespace MyApi.Modules.OAS.Audit.Models;

public enum OasAuditAction { insert, update, delete, correct }

/// <summary>Read-only from C#'s side — every row is written by the `oas_audit_row` trigger (spec: "logAudit n'a pas d'endpoint d'écriture"). Includes plugin-activation changes (v12 decision) since the trigger is attached to oas_plugin_activations too.</summary>
public class OasAuditLog
{
    public Guid Id { get; set; }
    public int? TenantId { get; set; }
    public string EntityTable { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public OasAuditAction Action { get; set; }
    public Guid? ActorId { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
