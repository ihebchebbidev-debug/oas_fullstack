using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MyApi.Modules.Roles.Services;
using System.Security.Claims;

namespace MyApi.Infrastructure
{
    /// <summary>
    /// Declarative RBAC for controller actions.
    ///
    /// Before this existed, every purchase / RS endpoint was protected by [Authorize]
    /// only — meaning ANY authenticated user of the tenant could create, edit or delete
    /// financial and fiscal records regardless of their role.
    ///
    /// Usage: [RequirePermission("purchases", "write")]
    /// MainAdminUser bypasses granular checks (same rule as ReportingController).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public string Module { get; }
        public string Action { get; }

        public RequirePermissionAttribute(string module, string action)
        {
            Module = module;
            Action = action;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    success = false,
                    error = new { code = "UNAUTHENTICATED", message = "Authentication required" }
                });
                return;
            }

            // Tenant owner / main admin: full access, no per-module grants stored.
            var userType = user.FindFirst("UserType")?.Value;
            if (string.Equals(userType, "MainAdminUser", StringComparison.OrdinalIgnoreCase))
                return;

            var idClaim = user.FindFirst("UserId")?.Value
                          ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var permissionService = context.HttpContext.RequestServices.GetService(typeof(IPermissionService)) as IPermissionService;
            var allowed = false;
            if (permissionService != null && int.TryParse(idClaim, out var userId))
            {
                allowed = await permissionService.UserHasPermissionAsync(userId, Module, Action);
            }

            if (!allowed)
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    error = new
                    {
                        code = "FORBIDDEN",
                        message = $"Missing permission '{Module}:{Action}'"
                    }
                })
                { StatusCode = StatusCodes.Status403Forbidden };
            }
        }
    }
}