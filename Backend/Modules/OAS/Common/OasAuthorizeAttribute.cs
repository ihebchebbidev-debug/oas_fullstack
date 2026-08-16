using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MyApi.Modules.OAS.Common;

/// <summary>
/// Role gate for OAS routes, keyed off the `oas_role` JWT claim (admin |
/// supervisor | operator — spec §8.0, §8.1). Distinct from the socle's
/// [RequirePermission] — OAS has no granular permission system, only these
/// three flat roles, and MainAdminUser does NOT bypass it (spec §8.0: OAS
/// auth is entirely separate from socle auth).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class OasAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <summary>Comma-separated OAS roles allowed to call this action, e.g. "admin,supervisor". Settable as a named argument: [OasAuthorize(Roles = "admin")].</summary>
    public string Roles { get; set; } = "admin,supervisor,operator";

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
            return Task.CompletedTask;
        }

        var allowedRoles = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var role = user.FindFirst("oas_role")?.Value;
        if (string.IsNullOrEmpty(role) || !allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(new { title = "forbidden", status = 403 })
            {
                StatusCode = 403
            };
        }

        return Task.CompletedTask;
    }
}
