using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Common.Models;

/// <summary>
/// Per-tenant activation state for the 13 OAS plugins (spec §5.2 table 16,
/// §1.5 plugin registry). Replaces the client's `oas.plugins.activations.v1`
/// localStorage key. isCore plugins can never be disabled; disabling a
/// plugin cascades to its dependents — both rules are enforced in
/// PluginActivationService, mirroring `activationStore.ts:59-88` exactly.
/// </summary>
public class OasPluginActivation : IOasTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TenantId { get; set; }
    public string PluginCode { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
}
