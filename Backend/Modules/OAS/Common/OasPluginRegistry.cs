namespace MyApi.Modules.OAS.Common;

public sealed record OasPluginManifest(string Code, bool IsCore, string[] DependsOn);

/// <summary>
/// Mirrors the frontend's auto-discovered registry (spec §1.5: 15
/// manifests — 13 functional + 2 shells, `registry.ts:11`). isCore plugins
/// can never be disabled; disabling a plugin cascades to everything that
/// depends on it, directly or transitively (`activationStore.ts:59-88`) —
/// both rules are reproduced exactly in OasPluginActivationService.
/// </summary>
public static class OasPluginRegistry
{
    public static readonly IReadOnlyList<OasPluginManifest> All = new[]
    {
        new OasPluginManifest("OA0001AUTH", true, Array.Empty<string>()),
        new OasPluginManifest("OA0002DASHBOARD", true, Array.Empty<string>()),
        new OasPluginManifest("OA0003SHOPFLOOR", true, Array.Empty<string>()),
        new OasPluginManifest("OA0004DECLARATIONS", false, new[] { "OA0003SHOPFLOOR", "OA0008REFERENTIALS" }),
        new OasPluginManifest("OA0005INTERVENTIONS", false, new[] { "OA0004DECLARATIONS" }),
        new OasPluginManifest("OA0006ALERTS", false, new[] { "OA0004DECLARATIONS" }),
        new OasPluginManifest("OA0007ASSIGNMENTS", false, new[] { "OA0003SHOPFLOOR" }),
        new OasPluginManifest("OA0008REFERENTIALS", true, Array.Empty<string>()),
        new OasPluginManifest("OA0009REPORTING", false, new[] { "OA0004DECLARATIONS" }),
        new OasPluginManifest("OA0010ANDON", false, new[] { "OA0003SHOPFLOOR" }),
        new OasPluginManifest("OA0011CONSOLE", true, Array.Empty<string>()),
        new OasPluginManifest("OA0012TRACEABILITY", false, new[] { "OA0004DECLARATIONS" }),
        new OasPluginManifest("OA0013DEMO", false, Array.Empty<string>()),
        new OasPluginManifest("OA1000WEBAPP", true, Array.Empty<string>()),
        new OasPluginManifest("OA1001MOBILEAPP", true, Array.Empty<string>()),
    };

    public static bool IsKnown(string code) => All.Any(p => p.Code == code);
    public static bool IsCore(string code) => All.FirstOrDefault(p => p.Code == code)?.IsCore ?? false;

    /// <summary>Every plugin (direct or transitive) that depends on <paramref name="code"/>.</summary>
    public static IEnumerable<string> TransitiveDependents(string code)
    {
        var seen = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(code);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var dependent in All.Where(p => p.DependsOn.Contains(current)))
            {
                if (seen.Add(dependent.Code)) queue.Enqueue(dependent.Code);
            }
        }
        return seen;
    }
}
