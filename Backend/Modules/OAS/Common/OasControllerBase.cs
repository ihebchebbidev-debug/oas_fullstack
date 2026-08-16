using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyApi.Modules.OAS.Common;

/// <summary>
/// Base class for every OAS controller (spec §4.1). Requires the OAS auth
/// scheme (never socle JWTs) and exposes the current OAS user's
/// identity/scope pulled from JWT claims set at login (spec §8.1/§8.2).
///
/// Deliberately does NOT set a [Route("api/oas/[controller]")] base route:
/// the [controller] token substitutes the PascalCase class name verbatim
/// (e.g. "PluginActivations"), but the spec's catalog (§6.1) is kebab-case
/// throughout (`/plugin-activations`, `/post-sessions`, `/cause-proposals`).
/// A global route-token transformer would fix this but risks touching MVC
/// conventions shared with the socle's own controllers — out of scope (spec
/// §3.2). Every concrete controller instead declares its own explicit
/// `[Route("api/oas/kebab-case-path")]`, still always under api/oas/* (spec
/// §4.1, enforced by the startup route-inventory test, spec §3.4).
///
/// Concrete controllers add [OasWorkspace("web"|"mobile")] and
/// [OasAuthorize(Roles="...")] as needed — this base only fixes what is
/// true for ALL of them.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = OasAuthSchemes.SchemeName)]
public abstract class OasControllerBase : ControllerBase
{
    private OasDbContext? _db;

    /// <summary>Request-scoped OasDbContext, bound to the *oas database resolved by OasTenantMiddleware for X-Tenant.</summary>
    protected OasDbContext Db => _db ??= HttpContext.RequestServices.GetRequiredService<OasDbContext>();

    protected Guid CurrentOasUserId
    {
        get
        {
            var claim = User.FindFirst("oas_user_id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : throw new InvalidOperationException("oas_user_id claim missing or invalid.");
        }
    }

    protected string CurrentOasRole => User.FindFirst("oas_role")?.Value ?? string.Empty;

    protected string CurrentOasWorkspace => User.FindFirst("oas_workspace")?.Value ?? string.Empty;

    protected Guid? CurrentScopeSiteId => TryParseGuidClaim("oas_scope_site_id");
    protected Guid? CurrentScopeZoneId => TryParseGuidClaim("oas_scope_zone_id");
    protected Guid? CurrentScopeLineId => TryParseGuidClaim("oas_scope_line_id");

    private Guid? TryParseGuidClaim(string type)
    {
        var claim = User.FindFirst(type)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// The row-level company scope (X-Target-Tenant), resolved by the
    /// socle's TenantMiddleware and stashed on HttpContext.Items exactly
    /// like ApplicationDbContext consumes it — OAS reads the SAME value,
    /// never a value parsed from the request body (spec §4.1).
    /// </summary>
    protected int CurrentTenantId => HttpContext.Items.TryGetValue("TenantId", out var v) && v is int i ? i : 0;

    protected ObjectResult Problem(int statusCode, string title, string? detail = null)
        => new ObjectResult(new ProblemDetails { Status = statusCode, Title = title, Detail = detail })
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
}
