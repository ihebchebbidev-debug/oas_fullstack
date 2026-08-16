using Microsoft.AspNetCore.Mvc.Filters;
using MyApi.Modules.OAS.Common.Services;

namespace MyApi.Modules.OAS.Common;

/// <summary>
/// Kill-switch (spec §3.4, §8.0): every controller for a non-core plugin
/// carries [OasPluginGate("OA00xx...")] and gets a 404 — not 403 — the
/// moment that plugin is disabled, exactly as if the routes didn't exist.
/// Core plugins never need this attribute since they can never be disabled.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class OasPluginGateAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _pluginCode;

    public OasPluginGateAttribute(string pluginCode) => _pluginCode = pluginCode;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var service = context.HttpContext.RequestServices.GetRequiredService<IOasPluginActivationService>();
        var tenantId = context.HttpContext.Items.TryGetValue("TenantId", out var v) && v is int i ? i : 0;

        if (!await service.IsEnabledAsync(tenantId, _pluginCode))
        {
            context.Result = new Microsoft.AspNetCore.Mvc.NotFoundResult();
            return;
        }

        await next();
    }
}
