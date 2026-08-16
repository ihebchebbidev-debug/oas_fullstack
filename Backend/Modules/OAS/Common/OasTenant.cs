namespace MyApi.Modules.OAS.Common;

/// <summary>
/// Helpers for the "*oas" tenant-slug convention (spec §1.2 bis). Every socle
/// tenant slug &lt;x&gt; has a corresponding OAS tenant &lt;x&gt;oas, served by its own
/// database (TENANT_&lt;X&gt;OAS_DATABASE_URL). Detection is purely lexical — never
/// used to choose a database directly, only to decide whether a request is an
/// OAS request at all and to derive the parent slug for user sync.
/// </summary>
public static class OasTenant
{
    private const string Suffix = "oas";

    public static bool IsOasSlug(string? slug)
        => !string.IsNullOrWhiteSpace(slug)
           && slug.Trim().EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// "krossieroas" → "krossier". Informational only (logging / commercial
    /// attachment) — never used to select a database. Caller must have
    /// already verified <see cref="IsOasSlug"/>.
    /// </summary>
    public static string BaseSlug(string slug)
    {
        var trimmed = slug.Trim();
        return trimmed[..^Suffix.Length];
    }
}
