using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MyApi.Modules.OAS.Common;

/// <summary>
/// Enforces the `oas_workspace` JWT claim against the workspace a controller
/// belongs to (spec §3.3, §8.0: `WebApp.tsx:105` / `MobileApp.tsx:315`
/// workspace guard, ported server-side). A console token is refused on
/// mobile-only routes and vice versa; `oas_workspace=both` (rare, e.g. a
/// supervisor account) passes either gate.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class OasWorkspaceAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Workspace { get; }

    public OasWorkspaceAttribute(string workspace) => Workspace = workspace;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
            return Task.CompletedTask;
        }

        var workspace = user.FindFirst("oas_workspace")?.Value;
        var ok = string.Equals(workspace, Workspace, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(workspace, "both", StringComparison.OrdinalIgnoreCase);

        if (!ok)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(new { title = "wrong_workspace", status = 403 })
            {
                StatusCode = 403
            };
        }

        return Task.CompletedTask;
    }
}
