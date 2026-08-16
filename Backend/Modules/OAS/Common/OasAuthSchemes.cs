namespace MyApi.Modules.OAS.Common;

/// <summary>
/// OAS issues its own JWTs, signed with the same key as the socle but with a
/// distinct audience per workspace (spec §3.3, §8.0): a shopfloor token
/// (aud=oas-shopfloor) is refused on console routes and vice versa. This is
/// registered as a second, named JWT Bearer scheme alongside the socle's
/// default scheme — never replacing or reconfiguring it (spec §3.2: "modifier
/// RequirePermissionAttribute ou PermissionService du socle" is forbidden;
/// adding an independent scheme touches neither).
/// </summary>
public static class OasAuthSchemes
{
    public const string SchemeName = "Oas";
    public const string ConsoleAudience = "oas-console";
    public const string ShopfloorAudience = "oas-shopfloor";
}
