using System.Net;
using System.Text.Json;
using Npgsql;

namespace MyApi.Infrastructure;

/// <summary>
/// Production-grade global exception handler.
/// Catches ALL unhandled exceptions and returns consistent JSON error responses
/// instead of crashing the process or leaking stack traces.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — don't log as error
            _logger.LogDebug("Request cancelled by client: {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 499; // Client Closed Request
        }
        catch (InvalidOperationException ex)
        {
            // Business logic errors → 400 Bad Request
            _logger.LogWarning(ex, "Business rule violation: {Message}", ex.Message);
            await WriteErrorResponse(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt: {Path}", context.Request.Path);
            await WriteErrorResponse(context, HttpStatusCode.Forbidden, "Access denied");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found: {Message}", ex.Message);
            await WriteErrorResponse(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Operation timed out: {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorResponse(context, HttpStatusCode.GatewayTimeout, "The operation timed out. Please try again.");
        }
        catch (Exception ex) when (TryGetSchemaDriftException(ex, out var schemaException))
        {
            // Repair the missing table/column immediately for this tenant database,
            // then ask the client to retry. DDL runs on a fresh connection, never
            // inside the failed request transaction.
            var tenant = context.Request.Headers[TenantMiddleware.TenantHeaderName].FirstOrDefault() ?? "default";
            _logger.LogCritical(schemaException,
                "Database schema drift for tenant {Tenant}: SqlState={SqlState}, DatabaseObject={DatabaseObject}",
                tenant,
                schemaException?.SqlState,
                schemaException?.ColumnName ?? schemaException?.TableName ?? "unknown");

            var repaired = false;
            var repairService = context.RequestServices.GetService<RuntimeSchemaRepair>();
            if (repairService != null)
            {
                repaired = await repairService.TryRepairAsync(tenant, context.RequestAborted);
            }

            context.Response.Headers["Retry-After"] = repaired ? "1" : "30";
            await WriteErrorResponse(
                context,
                HttpStatusCode.ServiceUnavailable,
                repaired
                    ? "The tenant database was just updated automatically. Please retry."
                    : "This tenant database is being updated. Please retry shortly.",
                "database_schema_out_of_date");
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            // Persist the technical detail so it is retrievable through /api/systemlogs.
            // Server stdout is not reachable from every environment, and without this an
            // unhandled 500 leaves no trace at all beyond the generic client message.
            await TryPersistErrorAsync(context, ex);

            var message = _env.IsDevelopment() 
                ? ex.Message 
                : "An internal error occurred. Please try again later.";
            
            await WriteErrorResponse(context, HttpStatusCode.InternalServerError, message);
        }
    }

    private static async Task TryPersistErrorAsync(HttpContext context, Exception exception)
    {
        try
        {
            var logService = context.RequestServices
                .GetService<MyApi.Modules.Shared.Services.ISystemLogService>();
            if (logService == null) return;

            var chain = new List<string>();
            for (var current = exception; current != null; current = current.InnerException)
            {
                var extra = current is PostgresException pg
                    ? $" [SqlState={pg.SqlState} Table={pg.TableName} Column={pg.ColumnName} Constraint={pg.ConstraintName}]"
                    : string.Empty;
                chain.Add($"{current.GetType().Name}: {current.Message}{extra}");
            }

            var stack = exception.StackTrace ?? string.Empty;
            if (stack.Length > 4000) stack = stack.Substring(0, 4000);

            await logService.LogErrorAsync(
                $"[500] {context.Request.Method} {context.Request.Path}: {chain[0]}",
                "Api",
                "other",
                details: JsonSerializer.Serialize(new
                {
                    path = context.Request.Path.Value,
                    method = context.Request.Method,
                    exceptions = chain,
                    stack
                }));
        }
        catch
        {
            // Diagnostics must never mask the original failure.
        }
    }


    private static bool TryGetSchemaDriftException(Exception exception, out PostgresException? postgresException)
    {
        Exception? current = exception;
        while (current != null)
        {
            if (current is PostgresException pg &&
                pg.SqlState is PostgresErrorCodes.UndefinedColumn or PostgresErrorCodes.UndefinedTable)
            {
                postgresException = pg;
                return true;
            }
            current = current.InnerException;
        }

        postgresException = null;
        return false;
    }

    private static async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message, string? code = null)
    {
        if (context.Response.HasStarted) return;

        var requestedHeaders = context.Request.Headers["Access-Control-Request-Headers"].FirstOrDefault();
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,PATCH,DELETE,OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = string.IsNullOrWhiteSpace(requestedHeaders)
            ? "Authorization,Content-Type,Accept,Origin,X-Requested-With,X-Tenant,X-Target-Tenant,x-tenant,x-target-tenant,apikey"
            : requestedHeaders;
        context.Response.Headers["Access-Control-Expose-Headers"] = "X-Total-Count,X-Page-Number,X-Page-Size,Content-Disposition";

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = (int)statusCode,
            error = statusCode.ToString(),
            code,
            message,
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
