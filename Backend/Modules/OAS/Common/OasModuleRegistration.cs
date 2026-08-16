using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyApi.Modules.OAS.Common.Realtime;
using MyApi.Modules.OAS.Common.Scope;
using MyApi.Modules.OAS.Common.Services;
using MyApi.Modules.OAS.ShopFloorAuth.Services;
using System.Text;

namespace MyApi.Modules.OAS.Common;

/// <summary>
/// Single DI entry point for the whole OAS module (spec §3.1) — one of the
/// 3 lines touching Program.cs. Everything else the module needs (a second
/// JWT scheme, the tenant-scoped DbContext, every sub-module's services) is
/// wired up here, never by editing Program.cs again.
/// </summary>
public static class OasModuleRegistration
{
    public static IServiceCollection AddOasModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        // Second, independent JWT Bearer scheme (spec §3.3, §8.0) — added
        // alongside the socle's default scheme via AddAuthentication()
        // (no default-scheme argument), never reconfiguring it.
        var jwtKey = configuration["Jwt:Key"] ?? "YourSuperSecretKeyHere12345";
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "MyApi";
        services.AddAuthentication().AddJwtBearer(OasAuthSchemes.SchemeName, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudiences = new[] { OasAuthSchemes.ConsoleAudience, OasAuthSchemes.ShopfloorAudience },
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromMinutes(1),
            };
            // spec §6.3: "SSE authentifié (JWT en query ou header)" — a browser
            // EventSource cannot set an Authorization header, so /api/oas/stream
            // must accept the token as ?access_token=; every other OAS route
            // still requires the header (this only fires for that one path).
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api/oas/stream"))
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token)) context.Token = token;
                    }
                    return Task.CompletedTask;
                },
            };
        });

        services.AddSingleton<IOasDbContextFactory, OasDbContextFactory>();
        services.AddSingleton<IOasSseBroadcaster, OasSseBroadcaster>();
        services.AddScoped<IOasScopeFilter, OasScopeFilter>();
        services.AddScoped<IOasPluginActivationService, OasPluginActivationService>();
        services.AddScoped<IOasAuthService, OasAuthService>();
        services.AddScoped<IOasUserJitSyncService, OasUserJitSyncService>();
        services.AddScoped<IOasTokenService, OasTokenService>();
        services.AddScoped<IOasShopFloorAuthService, OasShopFloorAuthService>();
        services.AddScoped<IOasOperatorService, OasOperatorService>();
        services.AddScoped<Hierarchy.Services.IOasHierarchyService, Hierarchy.Services.OasHierarchyService>();
        services.AddScoped<Equipments.Services.IOasEquipmentService, Equipments.Services.OasEquipmentService>();
        services.AddScoped<Cadences.Services.IOasCadenceService, Cadences.Services.OasCadenceService>();
        services.AddScoped<Products.Services.IOasProductService, Products.Services.OasProductService>();
        services.AddScoped<Causes.Services.IOasCauseService, Causes.Services.OasCauseService>();
        services.AddScoped<Shifts.Services.IOasShiftService, Shifts.Services.OasShiftService>();
        services.AddScoped<Teams.Services.IOasTeamService, Teams.Services.OasTeamService>();
        services.AddScoped<Imports.Services.IOasImportService, Imports.Services.OasImportService>();
        services.AddScoped<Lookups.Services.IOasLookupService, Lookups.Services.OasLookupService>();
        services.AddScoped<PostSessions.Services.IOasPostSessionService, PostSessions.Services.OasPostSessionService>();
        services.AddScoped<Assignments.Services.IOasAssignmentService, Assignments.Services.OasAssignmentService>();
        services.AddScoped<Declarations.Services.IOasDeclarationService, Declarations.Services.OasDeclarationService>();
        services.AddScoped<Changeovers.Services.IOasChangeoverService, Changeovers.Services.OasChangeoverService>();
        services.AddScoped<Quality.Services.IOasQualityService, Quality.Services.OasQualityService>();
        services.AddScoped<Events.Services.IOasEventService, Events.Services.OasEventService>();
        services.AddScoped<PostStates.Services.IOasPostStateService, PostStates.Services.OasPostStateService>();
        services.AddScoped<Sla.Services.IOasSlaService, Sla.Services.OasSlaService>();
        services.AddScoped<Interventions.Services.IOasInterventionService, Interventions.Services.OasInterventionService>();
        services.AddScoped<Offline.Services.IOasOfflineSyncService, Offline.Services.OasOfflineSyncService>();
        services.AddScoped<Kpi.Services.IOasKpiService, Kpi.Services.OasKpiService>();
        services.AddScoped<Audit.Services.IOasAuditService, Audit.Services.OasAuditService>();
        services.AddScoped<Integrations.Services.IOasIntegrationService, Integrations.Services.OasIntegrationService>();
        services.AddHttpClient("oas-integration-delivery");

        // Hosted services (spec §6.3) — replace the browser-tab timers
        // (eventStore.ts 30s sweep, session.ts 60s watchdog) with real
        // server-side background work that runs regardless of any tab
        // being open, across every provisioned *oas tenant database.
        services.AddHostedService<HostedServices.OasEscalationSweepHostedService>();
        services.AddHostedService<HostedServices.OasSessionWatchdogHostedService>();
        services.AddHostedService<Integrations.HostedServices.OasIntegrationDeliveryHostedService>();

        // Request-scoped OasDbContext bound to the *oas database for this
        // request's slug (resolved by OasTenantMiddleware, spec §1.2 bis)
        // and pre-scoped to the row-level TenantId the socle's
        // TenantMiddleware already resolved from X-Target-Tenant — mirrors
        // exactly how ApplicationDbContext itself is registered in
        // Program.cs.
        services.AddScoped(sp =>
        {
            var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext
                ?? throw new InvalidOperationException("OasDbContext resolved outside an HTTP request.");

            var slug = httpContext.Items["OasSlug"] as string
                ?? throw new InvalidOperationException("OasSlug not set — OasTenantMiddleware must run before OasDbContext is resolved.");

            var tenantId = httpContext.Items.TryGetValue("TenantId", out var v) && v is int i ? i : 0;

            var factory = sp.GetRequiredService<IOasDbContextFactory>();
            var ctx = factory.CreateDbContext(slug);
            ctx.SetTenantId(tenantId);
            return ctx;
        });

        return services;
    }
}
