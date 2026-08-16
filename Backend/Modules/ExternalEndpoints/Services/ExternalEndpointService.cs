using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyApi.Data;
using MyApi.Modules.ExternalEndpoints.DTOs;
using MyApi.Modules.ExternalEndpoints.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MyApi.Modules.ExternalEndpoints.Services
{
    public class ExternalEndpointService : IExternalEndpointService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ExternalEndpointService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory? _httpClientFactory;
        private readonly IWebhookForwardQueue? _forwardQueue;

        public ExternalEndpointService(ApplicationDbContext context, ILogger<ExternalEndpointService> logger,
            IServiceScopeFactory scopeFactory, IHttpClientFactory? httpClientFactory = null,
            IWebhookForwardQueue? forwardQueue = null)
        {
            _context = context;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _forwardQueue = forwardQueue;
        }

        // Plain key returned ONCE on create/regenerate. The DB only stores the hash.
        private const string HashPrefix = "h$";

        private static string GenerateApiKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return "ext_" + Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..40];
        }

        private static string HashApiKey(string plain)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(plain));
            return HashPrefix + Convert.ToHexString(hash);
        }

        // Constant-time verification. Supports legacy plaintext rows during migration.
        private static bool VerifyApiKey(string stored, string provided)
        {
            if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(provided)) return false;
            string a = stored;
            string b = stored.StartsWith(HashPrefix) ? HashApiKey(provided) : provided;
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            if (aBytes.Length != bBytes.Length) return false;
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }

        private static string GenerateSlug(string name)
        {
            var slug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            return slug + "-" + Guid.NewGuid().ToString("N")[..6];
        }

        // Mask the API key for list/get responses. The plain key is returned only
        // from CreateEndpointAsync, RegenerateKeyAsync, and the dedicated
        // RevealKeyAsync endpoint (which requires re-auth via [Authorize]).
        private static string MaskApiKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (key.Length <= 8) return new string('•', key.Length);
            var prefix = key.StartsWith("ext_") ? "ext_" : key[..Math.Min(4, key.Length)];
            var suffix = key[^4..];
            return $"{prefix}••••••••••••{suffix}";
        }

        private ExternalEndpointDto MapToDto(ExternalEndpoint e, int totalReceived = 0, int receivedToday = 0, DateTime? lastReceived = null, int totalSent = 0, int sentToday = 0)
        {
            return new ExternalEndpointDto
            {
                Id = e.Id,
                Name = e.Name,
                Slug = e.Slug,
                Description = e.Description,
                ApiKey = MaskApiKey(e.ApiKey),
                IsActive = e.IsActive,
                AllowedMethods = e.AllowedMethods,
                AllowedOrigins = e.AllowedOrigins,
                ExpectedSchema = e.ExpectedSchema,
                ResponseTemplate = e.ResponseTemplate,
                WebhookForwardUrl = e.WebhookForwardUrl,
                LogRetentionDays = e.LogRetentionDays,
                AcceptedContentTypes = e.AcceptedContentTypes,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                CreatedBy = e.CreatedBy,
                TotalReceived = totalReceived,
                ReceivedToday = receivedToday,
                LastReceived = lastReceived,
                TotalSent = totalSent,
                SentToday = sentToday,
            };
        }

        // ── Content-type helpers ────────────────────────────────────────────

        // Resolve canonical content-type token ("json", "xml", "form", "other")
        // from a raw MIME type string so the rest of the code never does
        // substring checks on "application/x-www-form-urlencoded" inline.
        private static string ResolveContentFormat(string? contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return "json"; // default assumption
            var ct = contentType.Split(';')[0].Trim().ToLowerInvariant();
            if (ct.Contains("json")) return "json";
            if (ct.Contains("xml")) return "xml";
            if (ct.Contains("x-www-form-urlencoded") || ct.Contains("form")) return "form";
            return "other";
        }

        // Returns true when the endpoint's AcceptedContentTypes allows the given format.
        // "any" / empty = accept everything.
        private static bool IsContentTypeAccepted(string acceptedContentTypes, string format)
        {
            if (string.IsNullOrWhiteSpace(acceptedContentTypes) || acceptedContentTypes.Trim() == "any") return true;
            var accepted = acceptedContentTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant()).ToList();
            if (accepted.Contains("any")) return true;
            return accepted.Contains(format);
        }

        // ── XML / form-urlencoded → JSON conversion ──────────────────────────

        // Convert an XML body to a flat JSON representation so the schema
        // validator and conversion preview can use consistent JSON logic.
        // The result is a JSON object whose keys mirror the XML child element
        // names of the root node (one level deep). Nested elements are preserved
        // as JSON objects. Attributes become "@attr" keys. Arrays are detected
        // by repeated element names.
        // Returns null when the input is not valid XML.
        private static string? TryXmlToJson(string? xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            try
            {
                var doc = XDocument.Parse(xml.Trim());
                var root = doc.Root;
                if (root == null) return null;
                var obj = XElementToDict(root);
                return System.Text.Json.JsonSerializer.Serialize(obj);
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<string, object?> XElementToDict(XElement el)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            // Attributes → "@attrName"
            foreach (var attr in el.Attributes())
                dict[$"@{attr.Name.LocalName}"] = (object?)attr.Value;

            var children = el.Elements().ToList();
            if (children.Count == 0)
            {
                // Leaf node — return value under the "value" key if attributes exist,
                // otherwise the caller will handle the text value.
                dict["value"] = el.Value;
                return dict;
            }

            // Group children by local name to detect repeated elements (arrays).
            var groups = children.GroupBy(c => c.Name.LocalName).ToList();
            foreach (var g in groups)
            {
                var items = g.ToList();
                if (items.Count > 1)
                {
                    // Array
                    dict[g.Key] = items.Select(i => {
                        var sub = i.Elements().Any() ? (object?)XElementToDict(i) : (object?)i.Value;
                        return sub;
                    }).ToList();
                }
                else
                {
                    var child = items[0];
                    if (child.Elements().Any())
                        dict[g.Key] = XElementToDict(child);
                    else
                        dict[g.Key] = (object?)child.Value;
                }
            }
            return dict;
        }

        // Convert application/x-www-form-urlencoded to a JSON object string.
        private static string? TryFormToJson(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var pairs = body.Split('&', StringSplitOptions.RemoveEmptyEntries);
                var dict = new Dictionary<string, object?>();
                foreach (var pair in pairs)
                {
                    var idx = pair.IndexOf('=');
                    if (idx < 0) continue;
                    var key = Uri.UnescapeDataString(pair[..idx].Replace('+', ' '));
                    var val = Uri.UnescapeDataString(pair[(idx + 1)..].Replace('+', ' '));
                    dict[key] = (object?)val;
                }
                return System.Text.Json.JsonSerializer.Serialize(dict);
            }
            catch { return null; }
        }

        // Normalise the raw body to JSON for downstream schema validation
        // and conversion preview regardless of the original content-type.
        private static string? NormaliseBodyToJson(string? body, string format)
        {
            return format switch
            {
                "xml" => TryXmlToJson(body),
                "form" => TryFormToJson(body),
                _ => body,   // already JSON (or "other" — passed through as-is)
            };
        }

        // SSRF protection: reject webhook forwards to private/loopback/metadata IPs.
        // Async DNS so we don't block the threadpool. NOTE: there is still a TOCTOU
        // window between this check and the worker's actual HTTP request — for
        // hard-mode SSRF protection, pin the resolved IP via a custom
        // SocketsHttpHandler.ConnectCallback in the named HttpClient registration.
        private static async Task<(bool ok, string? error)> ValidateWebhookUrlAsync(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return (true, null);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return (false, "Invalid URL");
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return (false, "URL must be http or https");
            var host = uri.Host.ToLowerInvariant();
            if (host == "localhost" || host == "0.0.0.0" || host.EndsWith(".local") || host.EndsWith(".internal"))
                return (false, "Loopback / internal hosts are not allowed");
            try
            {
                var addresses = await System.Net.Dns.GetHostAddressesAsync(host);
                foreach (var addr in addresses)
                {
                    var bytes = addr.GetAddressBytes();
                    if (System.Net.IPAddress.IsLoopback(addr)) return (false, "Loopback IPs are not allowed");
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        if (bytes[0] == 10) return (false, "Private IPs are not allowed");
                        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return (false, "Private IPs are not allowed");
                        if (bytes[0] == 192 && bytes[1] == 168) return (false, "Private IPs are not allowed");
                        if (bytes[0] == 169 && bytes[1] == 254) return (false, "Link-local / metadata IPs are not allowed");
                        if (bytes[0] == 127) return (false, "Loopback IPs are not allowed");
                    }
                    else if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        if (addr.IsIPv6LinkLocal || addr.IsIPv6SiteLocal) return (false, "Link-local IPs are not allowed");
                        if ((bytes[0] & 0xFE) == 0xFC) return (false, "Unique-local IPs are not allowed");
                        if (addr.IsIPv4MappedToIPv6)
                        {
                            var v4 = addr.MapToIPv4().GetAddressBytes();
                            if (v4[0] == 10 || v4[0] == 127 ||
                                (v4[0] == 172 && v4[1] >= 16 && v4[1] <= 31) ||
                                (v4[0] == 192 && v4[1] == 168) ||
                                (v4[0] == 169 && v4[1] == 254))
                                return (false, "Mapped private IPs are not allowed");
                        }
                    }
                }
            }
            catch
            {
                return (false, "Could not resolve hostname");
            }
            return (true, null);
        }

        // Clamp user-supplied page size — prevents OOM on `?limit=1000000`.
        private const int MaxPageSize = 200;
        private static int ClampLimit(int limit) => limit <= 0 ? 20 : Math.Min(limit, MaxPageSize);
        private static int ClampPage(int page) => page <= 0 ? 1 : page;

        // Origin enforcement: comma-separated allowlist; "*" or empty = any.
        // Normalize both stored and request origins so trailing slashes,
        // case differences, and missing schemes don't silently 403 valid clients.
        private static string NormalizeOrigin(string raw)
        {
            var s = raw.Trim().TrimEnd('/').ToLowerInvariant();
            if (s.Length == 0) return s;
            if (!s.StartsWith("http://") && !s.StartsWith("https://"))
                s = "https://" + s;
            return s;
        }

        private static bool IsOriginAllowed(string? allowedOrigins, string? originHeader)
        {
            if (string.IsNullOrWhiteSpace(allowedOrigins) || allowedOrigins.Trim() == "*") return true;
            if (string.IsNullOrWhiteSpace(originHeader)) return false;
            var requestOrigin = NormalizeOrigin(originHeader);
            var allowed = allowedOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeOrigin);
            return allowed.Any(a => a == "*" || a == requestOrigin);
        }

        // Resolve the single CORS origin to echo on the response. Returns "*"
        // when the endpoint allows any origin, the request origin (verbatim)
        // when it's in the allowlist, or null when it's not allowed.
        public static string? ResolveCorsOrigin(string? allowedOrigins, string? originHeader)
        {
            if (string.IsNullOrWhiteSpace(allowedOrigins) || allowedOrigins.Trim() == "*") return "*";
            if (string.IsNullOrWhiteSpace(originHeader)) return null;
            return IsOriginAllowed(allowedOrigins, originHeader) ? originHeader.Trim().TrimEnd('/') : null;
        }

        private static string ComputeHmacHex(string secret, string payload)
        {
            using var h = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(payload ?? string.Empty))).ToLowerInvariant();
        }

        // ── Ownership guard ─────────────────────────────────────────────────
        // Defense-in-depth. Every log-scoped operation queries
        // ExternalEndpoints through the global tenant filter — if it returns
        // false, the calling tenant has no business reading/mutating this
        // endpoint's logs even if the EndpointId guesses right.
        private async Task<bool> CurrentTenantOwnsEndpointAsync(int endpointId)
        {
            return await _context.ExternalEndpoints
                .AsNoTracking()
                .AnyAsync(e => e.Id == endpointId && !e.IsDeleted);
        }

        // ── ExpectedSchema validation ───────────────────────────────────────
        // Lightweight required-keys check. Stored as JSON, e.g.:
        //   { "required": ["fullName", "email", "phone"] }
        // Returns (true, null) when valid or when no schema is set.
        private static (bool ok, string? error) ValidateAgainstSchema(string? expectedSchema, string? body)
        {
            if (string.IsNullOrWhiteSpace(expectedSchema)) return (true, null);
            List<string>? required = null;
            try
            {
                using var schemaDoc = System.Text.Json.JsonDocument.Parse(expectedSchema);
                if (schemaDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    schemaDoc.RootElement.TryGetProperty("required", out var req) &&
                    req.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    required = req.EnumerateArray()
                        .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Invalid stored schema → don't block ingest; treat as "no schema".
                return (true, null);
            }
            if (required == null || required.Count == 0) return (true, null);
            if (string.IsNullOrWhiteSpace(body)) return (false, "Body is required");
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                    return (false, "Body must be a JSON object");
                var missing = new List<string>();
                foreach (var key in required)
                {
                    if (!doc.RootElement.TryGetProperty(key, out var v) ||
                        v.ValueKind == System.Text.Json.JsonValueKind.Null ||
                        (v.ValueKind == System.Text.Json.JsonValueKind.String && string.IsNullOrWhiteSpace(v.GetString())))
                    {
                        missing.Add(key);
                    }
                }
                if (missing.Count > 0)
                    return (false, "Missing required field(s): " + string.Join(", ", missing));
                return (true, null);
            }
            catch (System.Text.Json.JsonException)
            {
                return (false, "Body is not valid JSON");
            }
        }

        // ── Rate limiting (in-memory, per-slug + per-IP, sliding 1 min) ─────
        // Single-instance defense against probing/brute-force. Swap for Redis
        // if you scale horizontally.
        private const int RateLimitPerMinute = 60;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int count, DateTime windowStart)> _rateBuckets = new();
        private static bool RateLimitAllow(string slug, string? ip)
        {
            var key = $"{slug}|{ip ?? "anon"}";
            var now = DateTime.UtcNow;
            var bucket = _rateBuckets.AddOrUpdate(key,
                _ => (1, now),
                (_, cur) => now - cur.windowStart > TimeSpan.FromMinutes(1)
                    ? (1, now)
                    : (cur.count + 1, cur.windowStart));
            // Opportunistic prune so the dictionary doesn't grow unbounded.
            if (_rateBuckets.Count > 10_000)
            {
                foreach (var kv in _rateBuckets)
                    if (now - kv.Value.windowStart > TimeSpan.FromMinutes(5))
                        _rateBuckets.TryRemove(kv.Key, out _);
            }
            return bucket.count <= RateLimitPerMinute;
        }

        public async Task<PaginatedEndpointResponse> GetEndpointsAsync(string? search = null, string? status = null, int page = 1, int limit = 20)
        {
            page = ClampPage(page); limit = ClampLimit(limit);
            var query = _context.ExternalEndpoints.AsNoTracking().Where(e => !e.IsDeleted);

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(e => e.Name.ToLower().Contains(s) || e.Slug.ToLower().Contains(s));
            }

            if (status == "active") query = query.Where(e => e.IsActive);
            else if (status == "inactive") query = query.Where(e => !e.IsActive);

            var total = await query.CountAsync();
            var endpoints = await query.OrderByDescending(e => e.CreatedAt).Skip((page - 1) * limit).Take(limit).ToListAsync();

            var today = DateTime.UtcNow.Date;
            var endpointIds = endpoints.Select(e => e.Id).ToList();
            
            var logStats = await _context.ExternalEndpointLogs
                .Where(l => endpointIds.Contains(l.EndpointId))
                .GroupBy(l => l.EndpointId)
                .Select(g => new {
                    EndpointId = g.Key,
                    Total = g.Count(),
                    Today = g.Count(l => l.ReceivedAt >= today),
                    Last = g.Max(l => l.ReceivedAt)
                }).ToListAsync();

            var fwdStats = await _context.WebhookForwardJobs
                .Where(j => endpointIds.Contains(j.EndpointId) && j.Status == "succeeded")
                .GroupBy(j => j.EndpointId)
                .Select(g => new {
                    EndpointId = g.Key,
                    Total = g.Count(),
                    Today = g.Count(j => j.CompletedAt >= today)
                }).ToListAsync();

            var dtos = endpoints.Select(e =>
            {
                var stats = logStats.FirstOrDefault(s => s.EndpointId == e.Id);
                var fwd = fwdStats.FirstOrDefault(s => s.EndpointId == e.Id);
                return MapToDto(e, stats?.Total ?? 0, stats?.Today ?? 0, stats?.Last, fwd?.Total ?? 0, fwd?.Today ?? 0);
            }).ToList();

            return new PaginatedEndpointResponse
            {
                Endpoints = dtos,
                Pagination = new PaginationInfo { Page = page, Limit = limit, Total = total, TotalPages = (int)Math.Ceiling(total / (double)limit) }
            };
        }

        public async Task<ExternalEndpointDto?> GetEndpointByIdAsync(int id)
        {
            var e = await _context.ExternalEndpoints.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (e == null) return null;

            var today = DateTime.UtcNow.Date;
            var stats = await _context.ExternalEndpointLogs.Where(l => l.EndpointId == id)
                .GroupBy(l => 1)
                .Select(g => new { Total = g.Count(), Today = g.Count(l => l.ReceivedAt >= today), Last = g.Max(l => (DateTime?)l.ReceivedAt) })
                .FirstOrDefaultAsync();

            var fwdStats = await _context.WebhookForwardJobs
                .Where(j => j.EndpointId == id && j.Status == "succeeded")
                .GroupBy(j => 1)
                .Select(g => new { Total = g.Count(), Today = g.Count(j => j.CompletedAt >= today) })
                .FirstOrDefaultAsync();

            return MapToDto(e, stats?.Total ?? 0, stats?.Today ?? 0, stats?.Last, fwdStats?.Total ?? 0, fwdStats?.Today ?? 0);
        }

        public async Task<ExternalEndpointDto> CreateEndpointAsync(CreateExternalEndpointDto dto, string userId)
        {
            // SSRF guard
            var (ok, err) = await ValidateWebhookUrlAsync(dto.WebhookForwardUrl);
            if (!ok) throw new ArgumentException($"Invalid webhook URL: {err}");

            var slug = string.IsNullOrEmpty(dto.Slug) ? GenerateSlug(dto.Name) : dto.Slug;

            // Slug must be unique GLOBALLY (the receive endpoint is public and
            // resolves a slug across all tenants, then disambiguates by API key).
            var exists = await _context.ExternalEndpoints
                .IgnoreQueryFilters()
                .AnyAsync(e => e.Slug == slug && !e.IsDeleted);
            if (exists) slug = slug + "-" + Guid.NewGuid().ToString("N")[..4];

            var plainKey = GenerateApiKey();
            // Per-endpoint HMAC secret for signing outbound webhook forwards.
            var forwardSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var entity = new ExternalEndpoint
            {
                Name = dto.Name,
                Slug = slug,
                Description = dto.Description,
                ApiKey = HashApiKey(plainKey),
                IsActive = dto.IsActive,
                AllowedMethods = dto.AllowedMethods,
                AllowedOrigins = dto.AllowedOrigins,
                ExpectedSchema = dto.ExpectedSchema,
                ResponseTemplate = dto.ResponseTemplate,
                WebhookForwardUrl = dto.WebhookForwardUrl,
                ForwardSecret = forwardSecret,
                LogRetentionDays = Math.Clamp(dto.LogRetentionDays, 0, 3650),
                AcceptedContentTypes = string.IsNullOrWhiteSpace(dto.AcceptedContentTypes) ? "any" : dto.AcceptedContentTypes,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ExternalEndpoints.Add(entity);
            await _context.SaveChangesAsync();
            // Reveal the plain key ONCE on creation. After this it is unrecoverable.
            var dtoOut = MapToDto(entity);
            dtoOut.ApiKey = plainKey;
            return dtoOut;
        }

        public async Task<ExternalEndpointDto> UpdateEndpointAsync(int id, UpdateExternalEndpointDto dto, string userId)
        {
            var entity = await _context.ExternalEndpoints.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted)
                ?? throw new KeyNotFoundException("Endpoint not found");

            if (dto.WebhookForwardUrl != null)
            {
                var (ok, err) = await ValidateWebhookUrlAsync(dto.WebhookForwardUrl);
                if (!ok) throw new ArgumentException($"Invalid webhook URL: {err}");
            }

            if (dto.Name != null) entity.Name = dto.Name;
            if (dto.Description != null) entity.Description = dto.Description;
            if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;
            if (dto.AllowedMethods != null) entity.AllowedMethods = dto.AllowedMethods;
            if (dto.AllowedOrigins != null) entity.AllowedOrigins = dto.AllowedOrigins;
            if (dto.ExpectedSchema != null) entity.ExpectedSchema = dto.ExpectedSchema;
            if (dto.ResponseTemplate != null) entity.ResponseTemplate = dto.ResponseTemplate;
            if (dto.WebhookForwardUrl != null) entity.WebhookForwardUrl = dto.WebhookForwardUrl;
            if (dto.LogRetentionDays.HasValue) entity.LogRetentionDays = Math.Clamp(dto.LogRetentionDays.Value, 0, 3650);
            if (dto.AcceptedContentTypes != null) entity.AcceptedContentTypes = string.IsNullOrWhiteSpace(dto.AcceptedContentTypes) ? "any" : dto.AcceptedContentTypes;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (await GetEndpointByIdAsync(id))!;
        }

        public async Task<bool> DeleteEndpointAsync(int id, string userId)
        {
            var entity = await _context.ExternalEndpoints.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
            if (entity == null) return false;
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedBy = userId;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ExternalEndpointDto> RegenerateKeyAsync(int id, string userId)
        {
            var entity = await _context.ExternalEndpoints.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted)
                ?? throw new KeyNotFoundException("Endpoint not found");
            var plainKey = GenerateApiKey();
            entity.ApiKey = HashApiKey(plainKey);
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Build the response WITHOUT re-querying via GetEndpointByIdAsync.
            // The re-query was the most fragile part of this path: an EF
            // tracking conflict, a tenant query-filter mismatch, or a
            // GroupBy translation issue on empty log sets could throw and
            // surface to the controller as a generic 500 — even though the
            // key was already rotated and persisted successfully. Compute
            // the lightweight log stats inline and detach the entity to
            // guarantee a clean DTO containing the freshly-minted plain key.
            var today = DateTime.UtcNow.Date;
            int totalReceived = 0, receivedToday = 0;
            DateTime? lastReceived = null;
            try
            {
                totalReceived = await _context.ExternalEndpointLogs
                    .AsNoTracking().CountAsync(l => l.EndpointId == id);
                receivedToday = await _context.ExternalEndpointLogs
                    .AsNoTracking().CountAsync(l => l.EndpointId == id && l.ReceivedAt >= today);
                lastReceived = await _context.ExternalEndpointLogs
                    .AsNoTracking().Where(l => l.EndpointId == id)
                    .OrderByDescending(l => l.ReceivedAt)
                    .Select(l => (DateTime?)l.ReceivedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                // Stats are best-effort; never let them break key rotation.
                _logger.LogWarning(ex, "RegenerateKeyAsync: failed to load log stats for endpoint {Id}", id);
            }

            var dto = MapToDto(entity, totalReceived, receivedToday, lastReceived);
            dto.ApiKey = plainKey; // reveal ONCE
            return dto;
        }

        // Keys are hashed at rest. For legacy plaintext rows we still return the value
        // (one-time migration aid). New keys must be regenerated to be revealed.
        public async Task<string> RevealKeyAsync(int id)
        {
            var entity = await _context.ExternalEndpoints.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted)
                ?? throw new KeyNotFoundException("Endpoint not found");
            if (!entity.ApiKey.StartsWith(HashPrefix)) return entity.ApiKey;
            throw new InvalidOperationException("API key is hashed and cannot be revealed. Regenerate to obtain a new key.");
        }

        public async Task<ExternalEndpointStatsDto> GetStatsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var endpoints = _context.ExternalEndpoints.Where(e => !e.IsDeleted);
            return new ExternalEndpointStatsDto
            {
                TotalEndpoints = await endpoints.CountAsync(),
                ActiveEndpoints = await endpoints.CountAsync(e => e.IsActive),
                TotalReceivedToday = await _context.ExternalEndpointLogs.CountAsync(l => l.ReceivedAt >= today),
                TotalReceivedAll = await _context.ExternalEndpointLogs.CountAsync(),
                TotalSentToday = await _context.WebhookForwardJobs.CountAsync(j => j.Status == "succeeded" && j.CompletedAt >= today),
                TotalSentAll = await _context.WebhookForwardJobs.CountAsync(j => j.Status == "succeeded"),
            };
        }

        // Logs
        private static ExternalEndpointLogDto MapLogToDto(ExternalEndpointLog l) => new()
        {
            Id = l.Id, EndpointId = l.EndpointId, Method = l.Method, Headers = l.Headers,
            QueryString = l.QueryString, Body = l.Body, SourceIp = l.SourceIp,
            StatusCode = l.StatusCode, ResponseBody = l.ResponseBody, ReceivedAt = l.ReceivedAt,
            ProcessedAt = l.ProcessedAt, IsRead = l.IsRead,
            ContentType = l.ContentType, CompanyId = l.CompanyId
        };

        public async Task<PaginatedLogResponse> GetLogsAsync(int endpointId, int page = 1, int limit = 20, string? companyId = null)
        {
            page = ClampPage(page); limit = ClampLimit(limit);
            if (!await CurrentTenantOwnsEndpointAsync(endpointId))
            {
                return new PaginatedLogResponse
                {
                    Logs = new(),
                    Pagination = new PaginationInfo { Page = page, Limit = limit, Total = 0, TotalPages = 0 }
                };
            }
            var query = _context.ExternalEndpointLogs.AsNoTracking().Where(l => l.EndpointId == endpointId);

            // Optional company filter — lets the UI show only logs for a specific company / ERP tenant.
            if (!string.IsNullOrWhiteSpace(companyId))
                query = query.Where(l => l.CompanyId == companyId);

            var total = await query.CountAsync();
            var logs = await query.OrderByDescending(l => l.ReceivedAt).Skip((page - 1) * limit).Take(limit).ToListAsync();
            return new PaginatedLogResponse
            {
                Logs = logs.Select(MapLogToDto).ToList(),
                Pagination = new PaginationInfo { Page = page, Limit = limit, Total = total, TotalPages = (int)Math.Ceiling(total / (double)limit) }
            };
        }

        public async Task<ExternalEndpointLogDto?> GetLogByIdAsync(int endpointId, int logId)
        {
            if (!await CurrentTenantOwnsEndpointAsync(endpointId)) return null;
            var l = await _context.ExternalEndpointLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == logId && x.EndpointId == endpointId);
            if (l == null) return null;
            return MapLogToDto(l);
        }

        public async Task<bool> DeleteLogAsync(int endpointId, int logId)
        {
            if (!await CurrentTenantOwnsEndpointAsync(endpointId)) return false;
            var log = await _context.ExternalEndpointLogs.FirstOrDefaultAsync(l => l.Id == logId && l.EndpointId == endpointId);
            if (log == null) return false;
            _context.ExternalEndpointLogs.Remove(log);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ClearLogsAsync(int endpointId)
        {
            if (!await CurrentTenantOwnsEndpointAsync(endpointId)) return false;
            // ExecuteDeleteAsync issues a single DELETE — avoids loading the full
            // log set into memory (the previous RemoveRange approach OOMs at scale).
            await _context.ExternalEndpointLogs
                .Where(l => l.EndpointId == endpointId)
                .ExecuteDeleteAsync();
            return true;
        }

        public async Task<bool> MarkLogAsReadAsync(int endpointId, int logId)
        {
            if (!await CurrentTenantOwnsEndpointAsync(endpointId)) return false;
            var log = await _context.ExternalEndpointLogs.FirstOrDefaultAsync(l => l.Id == logId && l.EndpointId == endpointId);
            if (log == null) return false;
            log.IsRead = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // Public receive — anonymous; resolves slug across all tenants and
        // disambiguates by API key. Stamps the OWNING endpoint's TenantId on
        // the persisted log/job (the StampTenantIdOnNewEntities hook honors
        // explicit non-zero TenantIds for these two entity types).
        public async Task<(int statusCode, string responseBody)> ReceiveAsync(string slug, string method, string? headers, string? queryString, string? body, string? sourceIp, string? apiKey, string? originHeader = null, string? contentType = null, string? companyId = null)
        {
            // Rate-limit before doing any DB work.
            if (!RateLimitAllow(slug, sourceIp))
                return (429, "{\"error\":\"Too many requests\"}");

            if (string.IsNullOrEmpty(apiKey))
                return (401, "{\"error\":\"API key required\"}");

            var candidates = await _context.ExternalEndpoints
                .IgnoreQueryFilters()
                .Where(e => e.Slug == slug && !e.IsDeleted)
                .ToListAsync();

            if (candidates.Count == 0) return (404, "{\"error\":\"Endpoint not found\"}");

            ExternalEndpoint? endpoint = null;
            foreach (var c in candidates)
            {
                if (VerifyApiKey(c.ApiKey, apiKey)) endpoint = c;
            }
            if (endpoint == null) return (401, "{\"error\":\"Invalid API key\"}");
            if (!endpoint.IsActive) return (403, "{\"error\":\"Endpoint is inactive\"}");

            // Origin enforcement (CSV allowlist; "*" or empty = any). Normalized
            // both sides so trailing slashes / case differences don't 403 valid clients.
            if (!IsOriginAllowed(endpoint.AllowedOrigins, originHeader))
                return (403, "{\"error\":\"Origin not allowed\"}");

            var allowedMethods = endpoint.AllowedMethods.Split(',').Select(m => m.Trim().ToUpper()).ToList();
            if (!allowedMethods.Contains(method.ToUpper()))
                return (405, "{\"error\":\"Method not allowed\"}");

            // Content-type check — only enforced when AcceptedContentTypes is not "any"
            // and the request supplies a Content-Type header.
            var format = ResolveContentFormat(contentType);
            if (!string.IsNullOrWhiteSpace(contentType) && method.ToUpper() != "GET")
            {
                if (!IsContentTypeAccepted(endpoint.AcceptedContentTypes, format))
                    return (415, $"{{\"error\":\"Content-Type '{contentType}' is not accepted by this endpoint\"}}");
            }

            // Normalise body to JSON for schema validation (XML and form-urlencoded → JSON).
            // We keep the raw body in the log but validate against the normalised JSON.
            var normalised = method.ToUpper() != "GET" ? NormaliseBodyToJson(body, format) : null;

            // ExpectedSchema validation (lightweight required-keys check). Only
            // enforced for body-bearing methods; GET ingest is treated as a
            // "ping" with no payload contract.
            if (method.ToUpper() != "GET")
            {
                var (schemaOk, schemaErr) = ValidateAgainstSchema(endpoint.ExpectedSchema, normalised ?? body);
                if (!schemaOk)
                {
                    // Persist a 400 log so the tenant sees rejected attempts in their UI.
                    var rejected = new ExternalEndpointLog
                    {
                        TenantId = endpoint.TenantId,
                        EndpointId = endpoint.Id,
                        Method = method.ToUpper(),
                        Headers = headers,
                        QueryString = queryString,
                        Body = body,          // preserve raw body
                        SourceIp = sourceIp,
                        StatusCode = 400,
                        ResponseBody = "{\"success\":false,\"error\":\"" + schemaErr?.Replace("\"", "'") + "\"}",
                        ReceivedAt = DateTime.UtcNow,
                        ProcessedAt = DateTime.UtcNow,
                        ContentType = contentType,
                        CompanyId = companyId
                    };
                    _context.ExternalEndpointLogs.Add(rejected);
                    try { await _context.SaveChangesAsync(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to persist schema-rejection log for {Slug}", slug); }
                    return (400, rejected.ResponseBody);
                }
            }

            var log = new ExternalEndpointLog
            {
                TenantId = endpoint.TenantId,
                EndpointId = endpoint.Id,
                Method = method.ToUpper(),
                Headers = headers,
                QueryString = queryString,
                Body = body,                  // store the original raw body (XML, JSON, form)
                SourceIp = sourceIp,
                StatusCode = 200,
                ReceivedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                ContentType = contentType,
                CompanyId = companyId
            };

            var responseBody = endpoint.ResponseTemplate ?? "{\"success\":true,\"message\":\"Data received\"}";
            log.ResponseBody = responseBody;

            _context.ExternalEndpointLogs.Add(log);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(endpoint.WebhookForwardUrl))
            {
                var (ok, _) = await ValidateWebhookUrlAsync(endpoint.WebhookForwardUrl);
                if (ok)
                {
                    var job = new WebhookForwardJob
                    {
                        TenantId = endpoint.TenantId,
                        EndpointId = endpoint.Id,
                        LogId = log.Id,
                        ForwardUrl = endpoint.WebhookForwardUrl!,
                        Body = body,
                        // Snapshot the secret so secret rotation doesn't break in-flight retries.
                        Secret = endpoint.ForwardSecret,
                        Status = "pending",
                        NextAttemptAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                    };
                    _context.WebhookForwardJobs.Add(job);
                    try { await _context.SaveChangesAsync(); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to persist webhook forward job for log {LogId} — forward will not be attempted", log.Id);
                    }

                    if (_forwardQueue != null)
                    {
                        try { await _forwardQueue.EnqueueAsync(job.Id); }
                        catch (Exception ex) { _logger.LogDebug(ex, "Could not signal worker for job {JobId} (poll will pick it up)", job.Id); }
                    }
                }
                else
                {
                    _logger.LogWarning("Skipped webhook forward for endpoint {Slug}: URL failed SSRF validation", slug);
                }
            }

            return (200, responseBody);
        }

        // ----- Conversion: parse a log payload into suggested offer/sale fields.
        // Heuristic — accepts common shapes (form submissions, e-commerce orders).
        // The frontend pre-fills the existing CreateOffer/CreateSale form so the
        // user picks/confirms a Contact and saves via the existing modules.
        public async Task<ConvertLogPreviewDto?> PreviewConvertLogAsync(int endpointId, int logId)
        {
            if (!await CurrentTenantOwnsEndpointAsync(endpointId)) return null;
            var log = await _context.ExternalEndpointLogs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == logId && l.EndpointId == endpointId);
            if (log == null) return null;

            var preview = new ConvertLogPreviewDto { LogId = log.Id };
            if (string.IsNullOrWhiteSpace(log.Body)) return preview;

            // Normalise to JSON before running heuristic extraction so the same
            // logic handles JSON, XML, and form-urlencoded payloads uniformly.
            var format = ResolveContentFormat(log.ContentType);
            var jsonBody = NormaliseBodyToJson(log.Body, format) ?? log.Body;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(jsonBody);
                var root = doc.RootElement;
                preview.RawJson = System.Text.Json.JsonSerializer.Deserialize<object>(jsonBody);

                // Try a list of keys, return value + which key matched (null if none).
                (string? value, string? matchedKey) GetStr(params string[] keys)
                {
                    if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return (null, null);
                    foreach (var k in keys)
                        if (root.TryGetProperty(k, out var v)
                            && v.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var s = v.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) return (s.Trim(), k);
                        }
                    return (null, null);
                }

                // Confidence rule: the FIRST key in the list is the canonical name —
                // a match on it counts as "exact". Any other key is "inferred".
                void Set(string field, (string? value, string? matchedKey) hit, string canonical)
                {
                    if (hit.value == null) { preview.Confidence[field] = "none"; return; }
                    preview.Confidence[field] = hit.matchedKey == canonical ? "exact" : "inferred";
                }

                var name = GetStr("contactName", "name", "fullName", "full_name", "customerName", "customer_name");
                preview.ContactName = name.value;
                Set("contactName", name, "contactName");

                var email = GetStr("email", "customerEmail", "customer_email", "contactEmail");
                preview.Email = email.value;
                Set("email", email, "email");

                var phone = GetStr("phone", "phoneNumber", "phone_number", "tel", "mobile");
                preview.Phone = phone.value;
                Set("phone", phone, "phone");

                var address = GetStr("address", "shippingAddress", "shipping_address", "billingAddress", "billing_address", "location");
                preview.Address = address.value;
                Set("address", address, "address");

                var currency = GetStr("currency", "currencyCode", "currency_code");
                preview.Currency = currency.value;
                Set("currency", currency, "currency");

                var notes = GetStr("notes", "message", "description", "comment", "remarks");
                preview.Notes = notes.value;
                Set("notes", notes, "notes");

                // Items: look for "items" / "products" / "line_items" arrays
                System.Text.Json.JsonElement itemsArr = default;
                string? matchedItemsKey = null;
                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var k in new[] { "items", "products", "line_items", "lineItems" })
                    {
                        if (root.TryGetProperty(k, out itemsArr) && itemsArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                        { matchedItemsKey = k; break; }
                    }
                }

                if (matchedItemsKey != null)
                {
                    foreach (var it in itemsArr.EnumerateArray())
                    {
                        if (it.ValueKind != System.Text.Json.JsonValueKind.Object) continue;

                        string? descKey = null, qtyKey = null, priceKey = null;
                        string? desc = null;
                        if (it.TryGetProperty("description", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String) { desc = d.GetString(); descKey = "description"; }
                        else if (it.TryGetProperty("name", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String) { desc = n.GetString(); descKey = "name"; }
                        else if (it.TryGetProperty("title", out var ti) && ti.ValueKind == System.Text.Json.JsonValueKind.String) { desc = ti.GetString(); descKey = "title"; }

                        decimal? qty = null;
                        if (it.TryGetProperty("quantity", out var q) && q.TryGetDecimal(out var qv)) { qty = qv; qtyKey = "quantity"; }
                        else if (it.TryGetProperty("qty", out var q2) && q2.TryGetDecimal(out var qv2)) { qty = qv2; qtyKey = "qty"; }

                        decimal? price = null;
                        if (it.TryGetProperty("unitPrice", out var up) && up.TryGetDecimal(out var upv)) { price = upv; priceKey = "unitPrice"; }
                        else if (it.TryGetProperty("price", out var p) && p.TryGetDecimal(out var pv)) { price = pv; priceKey = "price"; }
                        else if (it.TryGetProperty("amount", out var a) && a.TryGetDecimal(out var av)) { price = av; priceKey = "amount"; }

                        var quantity = qty ?? 1m;
                        var unitPrice = price ?? 0m;
                        var allCanonical = descKey == "description" && qtyKey == "quantity" && priceKey == "unitPrice";

                        preview.Items.Add(new ConvertLogItemDto
                        {
                            Description = desc,
                            Quantity = quantity,
                            UnitPrice = unitPrice,
                            TotalPrice = quantity * unitPrice,
                            Confidence = allCanonical ? "exact" : "inferred",
                        });
                    }
                    preview.Confidence["items"] = preview.Items.All(i => i.Confidence == "exact") ? "exact" : "inferred";
                }
                else
                {
                    preview.Confidence["items"] = "none";
                }

                // TotalAmount: prefer an explicit key, else compute from items.
                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    string? totalKey = null;
                    decimal? total = null;
                    foreach (var k in new[] { "totalAmount", "total", "amount", "grandTotal", "grand_total" })
                    {
                        if (root.TryGetProperty(k, out var tv) && tv.TryGetDecimal(out var tvv))
                        { total = tvv; totalKey = k; break; }
                    }
                    if (total.HasValue)
                    {
                        preview.TotalAmount = total;
                        preview.Confidence["totalAmount"] = totalKey == "totalAmount" ? "exact" : "inferred";
                    }
                    else if (preview.Items.Count > 0)
                    {
                        preview.TotalAmount = preview.Items.Sum(i => (i.Quantity ?? 0) * (i.UnitPrice ?? 0));
                        preview.Confidence["totalAmount"] = "inferred";
                    }
                    else
                    {
                        preview.Confidence["totalAmount"] = "none";
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Body is not JSON — return the empty preview with RawJson=null so UI can show a "raw text" notice.
            }

            return preview;
        }
    }
}
