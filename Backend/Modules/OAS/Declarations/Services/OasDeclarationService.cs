using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Declarations.DTOs;
using MyApi.Modules.OAS.Declarations.Models;

namespace MyApi.Modules.OAS.Declarations.Services;

public interface IOasDeclarationService
{
    Task<OasDeclarationDto> CreateProductionAsync(int tenantId, Guid userId, OasProductionDeclarationRequestDto request);
    Task<OasDeclarationDto> CreateScrapAsync(int tenantId, Guid userId, OasScrapDeclarationRequestDto request);
    Task<(bool success, string? error, OasDeclarationDto? dto)> CorrectAsync(int tenantId, Guid actorId, string actorRole, Guid id, OasCorrectDeclarationRequestDto request);
    Task<IReadOnlyList<OasDeclarationDto>> GetAsync(int tenantId, Guid? postId, Guid? sessionId, DateTimeOffset? from, DateTimeOffset? to);
    Task<OasDeclarationDto?> GetOneAsync(int tenantId, Guid id);
}

/// <summary>
/// v12 decision (BL-045): correction window is 10 minutes from the
/// ORIGINAL declaration's OccurredAt — not 24h (an earlier draft of the
/// spec had this wrong; the shipped frontend, its own code comment, and
/// all 3 locale files agree on 10 min). v15: ownership is enforced from
/// the server's own record of who created the declaration
/// (CreatedBy), never a client-supplied field — a relayed-in operator
/// cannot correct the outgoing operator's entries just because the
/// session continued.
/// </summary>
public class OasDeclarationService : IOasDeclarationService
{
    private static readonly TimeSpan CorrectionWindow = TimeSpan.FromMinutes(10);

    private readonly OasDbContext _db;
    public OasDeclarationService(OasDbContext db) => _db = db;

    public async Task<OasDeclarationDto> CreateProductionAsync(int tenantId, Guid userId, OasProductionDeclarationRequestDto request)
    {
        var existing = await _db.Set<OasDeclaration>().FirstOrDefaultAsync(d => d.ClientEventId == request.ClientEventId);
        if (existing is not null) return ToDto(existing);

        var declaration = new OasDeclaration
        {
            TenantId = tenantId, ClientEventId = request.ClientEventId, Kind = OasDeclarationKind.production,
            PostSessionId = request.PostSessionId, PostId = request.PostId, UserId = userId,
            ProductionOrderId = request.ProductionOrderId, ProductId = request.ProductId,
            QuantityOk = request.QuantityOk, QuantityNok = request.QuantityNok, Note = request.Note,
            OccurredAt = request.OccurredAt, CreatedBy = userId,
        };
        _db.Set<OasDeclaration>().Add(declaration);
        await _db.SaveChangesAsync();
        return ToDto(declaration);
    }

    public async Task<OasDeclarationDto> CreateScrapAsync(int tenantId, Guid userId, OasScrapDeclarationRequestDto request)
    {
        var existing = await _db.Set<OasDeclaration>().FirstOrDefaultAsync(d => d.ClientEventId == request.ClientEventId);
        if (existing is not null) return ToDto(existing);

        var declaration = new OasDeclaration
        {
            TenantId = tenantId, ClientEventId = request.ClientEventId, Kind = OasDeclarationKind.scrap,
            PostSessionId = request.PostSessionId, PostId = request.PostId, UserId = userId,
            ProductionOrderId = request.ProductionOrderId, ProductId = request.ProductId,
            QuantityNok = request.QuantityNok, ScrapCauseId = request.ScrapCauseId, Note = request.Note,
            OccurredAt = request.OccurredAt, CreatedBy = userId,
        };
        _db.Set<OasDeclaration>().Add(declaration);
        await _db.SaveChangesAsync();
        return ToDto(declaration);
    }

    public async Task<(bool success, string? error, OasDeclarationDto? dto)> CorrectAsync(int tenantId, Guid actorId, string actorRole, Guid id, OasCorrectDeclarationRequestDto request)
    {
        var existingCorrection = await _db.Set<OasDeclaration>().FirstOrDefaultAsync(d => d.ClientEventId == request.ClientEventId);
        if (existingCorrection is not null) return (true, null, ToDto(existingCorrection));

        var original = await _db.Set<OasDeclaration>().FindAsync(id);
        if (original is null) return (false, "not_found", null);

        var isPrivileged = actorRole is "admin" or "supervisor";
        if (!isPrivileged && original.CreatedBy != actorId)
        {
            return (false, "not_your_declaration", null);
        }

        if (DateTimeOffset.UtcNow - original.OccurredAt > CorrectionWindow)
        {
            return (false, "correction_window_expired", null);
        }

        // Append-only (spec §5.1 trigger): a correction is a NEW row, the
        // original's QuantityOk/QuantityNok/OccurredAt are never touched —
        // trg_oas_decl_correction marks original.IsCorrected via AFTER INSERT.
        var correction = new OasDeclaration
        {
            TenantId = tenantId, ClientEventId = request.ClientEventId, Kind = original.Kind,
            PostSessionId = original.PostSessionId, PostId = original.PostId, LineId = original.LineId,
            UserId = original.UserId, ProductionOrderId = original.ProductionOrderId, ProductId = original.ProductId,
            QuantityOk = request.QuantityOk ?? original.QuantityOk, QuantityNok = request.QuantityNok ?? original.QuantityNok,
            ScrapCauseId = original.ScrapCauseId, OccurredAt = original.OccurredAt,
            CorrectsId = original.Id, CorrectionReason = request.Reason, CreatedBy = actorId,
        };
        _db.Set<OasDeclaration>().Add(correction);
        await _db.SaveChangesAsync();

        return (true, null, ToDto(correction));
    }

    public async Task<IReadOnlyList<OasDeclarationDto>> GetAsync(int tenantId, Guid? postId, Guid? sessionId, DateTimeOffset? from, DateTimeOffset? to)
    {
        var q = _db.Set<OasDeclaration>().AsQueryable();
        if (postId is not null) q = q.Where(d => d.PostId == postId);
        if (sessionId is not null) q = q.Where(d => d.PostSessionId == sessionId);
        if (from is not null) q = q.Where(d => d.OccurredAt >= from);
        if (to is not null) q = q.Where(d => d.OccurredAt <= to);

        var rows = await q.OrderByDescending(d => d.OccurredAt).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<OasDeclarationDto?> GetOneAsync(int tenantId, Guid id)
    {
        var d = await _db.Set<OasDeclaration>().FindAsync(id);
        return d is null ? null : ToDto(d);
    }

    private static OasDeclarationDto ToDto(OasDeclaration d) => new()
    {
        Id = d.Id, Kind = d.Kind.ToString(), PostSessionId = d.PostSessionId, PostId = d.PostId, UserId = d.UserId,
        ProductionOrderId = d.ProductionOrderId, ProductId = d.ProductId, QuantityOk = d.QuantityOk, QuantityNok = d.QuantityNok,
        ScrapCauseId = d.ScrapCauseId, Note = d.Note, OccurredAt = d.OccurredAt, CorrectsId = d.CorrectsId, IsCorrected = d.IsCorrected,
        CorrectionReason = d.CorrectionReason, CreatedBy = d.CreatedBy,
    };
}
