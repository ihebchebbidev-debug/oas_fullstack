using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common.Realtime;
using System.Threading.Channels;

namespace MyApi.Modules.OAS.Common;

/// <summary>
/// Minimal-route endpoints that don't fit a resource controller (spec §3.1):
/// GET /api/oas/health and GET /api/oas/stream (SSE). The third of the 3
/// lines touching Program.cs.
/// </summary>
public static class OasEndpoints
{
    /// <summary>
    /// Tables expected to exist once public/OAS-SQL/001..004 have been run.
    /// Extend this list as later lots add tables (spec §1.2 bis point 6) —
    /// it is the single place §11 criterion 14's health check reads from.
    /// </summary>
    private static readonly string[] ExpectedTables =
    {
        "oas_users", "oas_plugin_activations", "oas_schema_migrations",
    };

    public static IEndpointRouteBuilder MapOasEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Exempt from the *oas slug requirement (spec §1.2 bis point 5) so
        // it can be probed before a client even knows which slug to send.
        endpoints.MapGet("/api/oas/health", HealthAsync).AllowAnonymous();

        endpoints.MapGet("/api/oas/stream", StreamAsync)
            .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { AuthenticationSchemes = OasAuthSchemes.SchemeName });

        return endpoints;
    }

    private static async Task<IResult> HealthAsync(HttpContext context, IOasDbContextFactory dbFactory, ILoggerFactory loggerFactory)
    {
        var slug = context.Request.Headers[Infrastructure.TenantMiddleware.TenantHeaderName].FirstOrDefault()?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(slug) || !OasTenant.IsOasSlug(slug))
        {
            return Results.Json(new { status = "no_tenant", message = "Send X-Tenant: <slug>oas to check a specific database." });
        }

        try
        {
            await using var db = dbFactory.CreateDbContext(slug);
            var present = new List<string>();
            var missing = new List<string>();

            foreach (var table in ExpectedTables)
            {
                var exists = await TableExistsAsync(db, table);
                (exists ? present : missing).Add(table);
            }

            return Results.Json(new
            {
                status = missing.Count == 0 ? "ok" : "incomplete",
                tenant = slug,
                tablesPresent = present,
                tablesMissing = missing,
            }, statusCode: missing.Count == 0 ? 200 : 503);
        }
        catch (OasTenantNotProvisionedException ex)
        {
            return Results.Json(new { status = "not_provisioned", tenant = ex.Slug }, statusCode: 503);
        }
        catch (Exception ex)
        {
            var logger = loggerFactory.CreateLogger("OasHealth");
            logger.LogError(ex, "🏭 OAS health check failed for tenant '{Slug}'", slug);
            return Results.Json(new { status = "error", tenant = slug, message = "Could not reach the database." }, statusCode: 503);
        }
    }

    private static async Task<bool> TableExistsAsync(OasDbContext db, string tableName)
    {
        try
        {
            var count = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name = {0}",
                tableName).FirstOrDefaultAsync();
            return count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task StreamAsync(HttpContext context, IOasSseBroadcaster broadcaster)
    {
        var slug = context.Items["OasSlug"] as string;
        if (string.IsNullOrEmpty(slug))
        {
            context.Response.StatusCode = 400;
            return;
        }
        var tenantId = context.Items.TryGetValue("TenantId", out var v) && v is int i ? i : 0;

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no"; // disable reverse-proxy buffering so events flush immediately

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });
        var subscriptionId = broadcaster.Subscribe(slug, tenantId, channel);
        var ct = context.RequestAborted;

        try
        {
            var lastEventId = context.Request.Headers["Last-Event-ID"].FirstOrDefault();
            _ = lastEventId; // spec §6.3: reconnection replay is a Lot-5 concern once event history exists to replay from.

            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(15));
            var heartbeatTask = Task.Run(async () =>
            {
                while (await heartbeat.WaitForNextTickAsync(ct))
                {
                    await channel.Writer.WriteAsync(": ping\n\n", ct);
                }
            }, ct);

            await foreach (var message in channel.Reader.ReadAllAsync(ct))
            {
                var payload = message.StartsWith(": ping") ? message : $"data: {message}\n\n";
                await context.Response.WriteAsync(payload, ct);
                await context.Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnected — normal SSE teardown
        }
        finally
        {
            broadcaster.Unsubscribe(subscriptionId);
        }
    }
}
