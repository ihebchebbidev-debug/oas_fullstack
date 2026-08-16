using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Hierarchy.Models;
using MyApi.Modules.OAS.PostSessions.DTOs;
using MyApi.Modules.OAS.PostSessions.Models;

namespace MyApi.Modules.OAS.PostSessions.Services;

public interface IOasPostSessionService
{
    Task<(bool success, string? error, OasPostSessionDto? dto)> OpenAsync(int tenantId, Guid userId, OasOpenSessionRequestDto request);
    Task<(bool success, string? error, OasPostSessionDto? dto)> RelayAsync(int tenantId, Guid sessionId, OasRelaySessionRequestDto request);
    Task<(bool success, string? error, OasPostSessionDto? dto)> CloseAsync(int tenantId, Guid sessionId, OasCloseSessionRequestDto request);
    Task<OasPostSessionDto?> GetActiveAsync(int tenantId, Guid userId);
    Task<OasScanResultDto> ScanAsync(int tenantId, OasScanRequestDto request);
}

/// <summary>
/// v15 critical fix: the frontend's `openSession()` silently overwrote an
/// active session with pending unsynced data (`session.ts:194-229`,
/// `ScanPage.tsx` had no active-session guard unlike every other mobile
/// screen). The server is the only real enforcement point — `OpenAsync`
/// refuses (409) a new session while one is already active for this user
/// or post, unless `ForceRelay` is explicitly set, in which case the old
/// session is closed (not silently discarded) before the new one opens.
/// </summary>
public class OasPostSessionService : IOasPostSessionService
{
    private readonly OasDbContext _db;
    public OasPostSessionService(OasDbContext db) => _db = db;

    public async Task<(bool success, string? error, OasPostSessionDto? dto)> OpenAsync(int tenantId, Guid userId, OasOpenSessionRequestDto request)
    {
        var existingByClientId = await _db.Set<OasPostSession>().FirstOrDefaultAsync(s => s.ClientEventId == request.ClientEventId);
        if (existingByClientId is not null) return (true, null, ToDto(existingByClientId)); // idempotent replay

        var activeForUser = await _db.Set<OasPostSession>().FirstOrDefaultAsync(s => s.UserId == userId && s.EndedAt == null);
        var activeForPost = await _db.Set<OasPostSession>().FirstOrDefaultAsync(s => s.PostId == request.PostId && s.EndedAt == null && s.UserId != userId);

        if (activeForPost is not null && !request.ForceRelay)
        {
            return (false, "post_already_has_active_session", null);
        }

        if (activeForUser is not null)
        {
            if (!request.ForceRelay) return (false, "user_already_has_active_session", null);
            activeForUser.EndedAt = DateTimeOffset.UtcNow;
        }

        if (activeForPost is not null && request.ForceRelay)
        {
            activeForPost.EndedAt = DateTimeOffset.UtcNow;
        }

        var session = new OasPostSession
        {
            TenantId = tenantId, ClientEventId = request.ClientEventId, PostId = request.PostId, UserId = userId,
            AssignmentId = request.AssignmentId, ProductionOrderId = request.ProductionOrderId, ShiftTemplateId = request.ShiftTemplateId,
            StartedAt = DateTimeOffset.UtcNow, StartedVia = request.StartedVia,
        };
        _db.Set<OasPostSession>().Add(session);
        await _db.SaveChangesAsync();

        return (true, null, ToDto(session));
    }

    public async Task<(bool success, string? error, OasPostSessionDto? dto)> RelayAsync(int tenantId, Guid sessionId, OasRelaySessionRequestDto request)
    {
        var session = await _db.Set<OasPostSession>().FindAsync(sessionId);
        if (session is null) return (false, "not_found", null);
        if (session.EndedAt is not null) return (false, "session_already_closed", null);

        // Relay keeps the SAME session row (and therefore its post,
        // production order, and any declarations already tied to it) —
        // only the operator changes. This is what actually preserves
        // in-flight work across a handover, unlike close+reopen.
        session.UserId = request.NewUserId;
        await _db.SaveChangesAsync();
        return (true, null, ToDto(session));
    }

    public async Task<(bool success, string? error, OasPostSessionDto? dto)> CloseAsync(int tenantId, Guid sessionId, OasCloseSessionRequestDto request)
    {
        var session = await _db.Set<OasPostSession>().FindAsync(sessionId);
        if (session is null) return (false, "not_found", null);

        if (session.EndedAt is not null) return (true, null, ToDto(session)); // idempotent

        session.EndedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return (true, null, ToDto(session));
    }

    public async Task<OasPostSessionDto?> GetActiveAsync(int tenantId, Guid userId)
    {
        var session = await _db.Set<OasPostSession>().FirstOrDefaultAsync(s => s.UserId == userId && s.EndedAt == null);
        return session is null ? null : ToDto(session);
    }

    public async Task<OasScanResultDto> ScanAsync(int tenantId, OasScanRequestDto request)
    {
        var code = ExtractPostCode(request.Code);
        if (code is null) return new OasScanResultDto { Resolved = false };

        var post = await _db.Set<OasPost>().FirstOrDefaultAsync(p => p.Code == code);
        return post is null
            ? new OasScanResultDto { Resolved = false }
            : new OasScanResultDto { Resolved = true, PostId = post.Id, PostCode = post.Code };
    }

    /// <summary>Mirrors the 4 formats `parsePostCode` (session.ts:141) accepts: raw code, `oas://post/&lt;code&gt;`, a URL with `?post=`/`?code=`, or the code embedded as the last path segment.</summary>
    private static string? ExtractPostCode(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return null;

        if (trimmed.StartsWith("oas://post/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed["oas://post/".Length..].Trim('/');
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var fromQuery = query["post"] ?? query["code"];
            if (!string.IsNullOrEmpty(fromQuery)) return fromQuery;

            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length > 0 && !string.IsNullOrEmpty(segments[^1])) return segments[^1];
        }

        return trimmed;
    }

    private static OasPostSessionDto ToDto(OasPostSession s) => new()
    {
        Id = s.Id, PostId = s.PostId, UserId = s.UserId, AssignmentId = s.AssignmentId,
        ProductionOrderId = s.ProductionOrderId, ShiftTemplateId = s.ShiftTemplateId,
        StartedAt = s.StartedAt, EndedAt = s.EndedAt,
    };
}
