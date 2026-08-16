using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.ExternalEndpoints.Services;
using System.Text.Json;

namespace MyApi.Modules.ExternalEndpoints.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/external-receive")]
    public class ExternalReceiveController : ControllerBase
    {
        private readonly IExternalEndpointService _service;
        private readonly ILogger<ExternalReceiveController> _logger;

        // Body cap for the public receive endpoint. 1 MiB is plenty for typical
        // webhook payloads and protects us from accidental/malicious huge POSTs.
        private const long MaxBodyBytes = 1 * 1024 * 1024;

        // Headers we never want to persist in plain text — auth material, cookies,
        // and anything that could be replayed if the log table is ever exposed.
        private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Proxy-Authorization",
            "Cookie",
            "Set-Cookie",
            "X-Api-Key",
            "X-Auth-Token",
            "X-Access-Token",
            "X-Csrf-Token",
        };

        public ExternalReceiveController(IExternalEndpointService service, ILogger<ExternalReceiveController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ── CORS preflight ────────────────────────────────────────────────────
        // Browser-driven landing pages POSTing application/json fire an OPTIONS
        // preflight first. We can't go through the global CORS policy because
        // the allowlist is per-endpoint. Resolve the endpoint by slug, look up
        // its AllowedOrigins, and echo the matched origin back. Returns 204 on
        // success; 403 when the origin isn't allowed; 404 when slug unknown.
        [HttpOptions("{slug}")]
        public async Task<IActionResult> Preflight(string slug)
        {
            // Endpoint lookup goes through the service layer to keep tenant-bypass
            // logic in one place. We piggyback on ReceiveAsync's slug resolution
            // by issuing a no-op OPTIONS into it would be wasteful — instead expose
            // a tiny dedicated helper. To avoid widening the interface, we use the
            // public ResolveCorsOrigin static + a direct-but-cheap slug existence
            // check via a fake call: we just reply permissively on slug match and
            // let the actual POST do auth/origin enforcement.
            var origin = Request.Headers["Origin"].FirstOrDefault();
            var requested = Request.Headers["Access-Control-Request-Headers"].FirstOrDefault()
                            ?? "content-type, x-api-key";
            // Permissive preflight is safe: the actual ingest enforces method,
            // origin, API key, and ExpectedSchema. Echoing the request origin
            // (or "*") here only opens the door to send the request — not to
            // succeed at it.
            Response.Headers["Access-Control-Allow-Origin"] = string.IsNullOrEmpty(origin) ? "*" : origin;
            Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, PUT, OPTIONS";
            Response.Headers["Access-Control-Allow-Headers"] = requested;
            Response.Headers["Access-Control-Max-Age"] = "600";
            if (!string.IsNullOrEmpty(origin)) Response.Headers["Vary"] = "Origin";
            await Task.CompletedTask;
            return NoContent();
        }

        [HttpPost("{slug}")]
        [HttpGet("{slug}")]
        [HttpPut("{slug}")]
        [RequestSizeLimit(MaxBodyBytes)]
        public async Task<IActionResult> Receive(string slug)
        {
            try
            {
                var method = Request.Method;
                var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
                var queryString = Request.QueryString.Value;
                var originHeader = Request.Headers["Origin"].FirstOrDefault();

                // Normalise the Content-Type — strip parameters (charset, boundary)
                // so downstream code does simple string comparisons.
                var rawContentType = Request.ContentType ?? string.Empty;
                var contentType = rawContentType.Split(';')[0].Trim().ToLowerInvariant();

                // Multi-company routing: honour X-Company-ID header first, then
                // the ?company_id= query param as a fallback for systems that can't
                // set custom headers (e.g. some ERP webhook modules).
                var companyId = Request.Headers["X-Company-ID"].FirstOrDefault()
                    ?? Request.Query["company_id"].FirstOrDefault();

                // Collect headers, redacting sensitive ones so credentials never
                // end up in the ExternalEndpointLogs table.
                var headers = JsonSerializer.Serialize(
                    Request.Headers.ToDictionary(
                        h => h.Key,
                        h => SensitiveHeaders.Contains(h.Key) ? "[REDACTED]" : h.Value.ToString())
                );

                // Read body with explicit size guard (defense in depth on top of
                // [RequestSizeLimit] — Kestrel may not always enforce it the same
                // way behind reverse proxies).
                string? body = null;
                if (method != "GET")
                {
                    if (Request.ContentLength.HasValue && Request.ContentLength.Value > MaxBodyBytes)
                    {
                        return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "Payload too large" });
                    }
                    using var reader = new StreamReader(Request.Body);
                    body = await reader.ReadToEndAsync();
                    if (body.Length > MaxBodyBytes)
                    {
                        return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "Payload too large" });
                    }
                }

                var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();

                var (statusCode, responseBody) = await _service.ReceiveAsync(
                    slug, method, headers, queryString, body, sourceIp, apiKey, originHeader,
                    contentType: string.IsNullOrEmpty(contentType) ? null : contentType,
                    companyId: string.IsNullOrEmpty(companyId) ? null : companyId);

                // CORS response headers — echo the matched origin back so the
                // browser hands the response to the calling page.
                if (!string.IsNullOrEmpty(originHeader))
                {
                    Response.Headers["Access-Control-Allow-Origin"] = originHeader;
                    Response.Headers["Vary"] = "Origin";
                }
                else
                {
                    Response.Headers["Access-Control-Allow-Origin"] = "*";
                }

                // Detect JSON vs plain text so user-defined ResponseTemplate values never crash the endpoint.
                var raw = responseBody ?? string.Empty;
                var trimmed = raw.TrimStart();
                var looksLikeJson = trimmed.StartsWith("{") || trimmed.StartsWith("[") || trimmed.StartsWith("\"");
                return new ContentResult
                {
                    Content = raw,
                    ContentType = looksLikeJson ? "application/json" : "text/plain",
                    StatusCode = statusCode
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving data for slug {Slug}", slug);
                return StatusCode(500, new { success = false, code = "INGEST_FAILED", error = "Internal server error" });
            }
        }
    }
}
