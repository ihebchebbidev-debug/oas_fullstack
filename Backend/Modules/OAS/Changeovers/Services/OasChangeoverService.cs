using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Changeovers.DTOs;
using MyApi.Modules.OAS.Changeovers.Models;
using MyApi.Modules.OAS.Common;
using System.Text.Json;

namespace MyApi.Modules.OAS.Changeovers.Services;

public interface IOasChangeoverService
{
    Task<(bool success, string? error, OasChangeoverDto? dto)> CreateOrUpdateAsync(int tenantId, Guid userId, OasChangeoverRequestDto request);
    Task<(bool success, string? error, OasChangeoverDto? dto)> FinishAsync(int tenantId, Guid actorId, Guid id);
    Task<IReadOnlyList<OasChangeoverDto>> GetAsync(int tenantId, Guid? postId, string? status);
}

/// <summary>
/// Default 5-step checklist mirrors ChangeoverPage.tsx's fixed sequence.
/// v12/v15 decision: `FinishAsync` refuses (409) unless every step is
/// done — closes the "resume production" bypass that let an operator
/// close an abandoned changeover without completing it.
/// </summary>
public class OasChangeoverService : IOasChangeoverService
{
    private static readonly (string Id, string Label)[] DefaultSteps =
    {
        ("clear", "Clear previous product/tooling"),
        ("setup", "Install new tooling/fixtures"),
        ("adjust", "Adjust parameters"),
        ("first_part", "Produce & validate first part"),
        ("cleanup", "Clean up work area"),
    };

    private readonly OasDbContext _db;
    public OasChangeoverService(OasDbContext db) => _db = db;

    public async Task<(bool success, string? error, OasChangeoverDto? dto)> CreateOrUpdateAsync(int tenantId, Guid userId, OasChangeoverRequestDto request)
    {
        var existing = await _db.Set<OasChangeover>().FirstOrDefaultAsync(c => c.ClientEventId == request.ClientEventId);
        if (existing is not null)
        {
            if (existing.EndedAt is not null) return (true, null, ToDto(existing)); // already finished — idempotent no-op
            if (request.Steps is not null) existing.Steps = JsonSerializer.Serialize(request.Steps);
            await _db.SaveChangesAsync();
            return (true, null, ToDto(existing));
        }

        var alreadyOpenForPost = await _db.Set<OasChangeover>().AnyAsync(c => c.PostId == request.PostId && c.EndedAt == null);
        if (alreadyOpenForPost) return (false, "post_already_has_open_changeover", null);

        var steps = request.Steps ?? DefaultSteps.Select(s => new OasChangeoverStepDto { Id = s.Id, Label = s.Label, Done = false }).ToList();

        var changeover = new OasChangeover
        {
            TenantId = tenantId, ClientEventId = request.ClientEventId, PostId = request.PostId,
            FromProductId = request.FromProductId, ToProductId = request.ToProductId, ProductionOrderId = request.ProductionOrderId,
            StartedAt = DateTimeOffset.UtcNow, TargetMin = request.TargetMin, Steps = JsonSerializer.Serialize(steps), StartedBy = userId,
        };
        _db.Set<OasChangeover>().Add(changeover);
        await _db.SaveChangesAsync();
        return (true, null, ToDto(changeover));
    }

    public async Task<(bool success, string? error, OasChangeoverDto? dto)> FinishAsync(int tenantId, Guid actorId, Guid id)
    {
        var changeover = await _db.Set<OasChangeover>().FindAsync(id);
        if (changeover is null) return (false, "not_found", null);
        if (changeover.EndedAt is not null) return (true, null, ToDto(changeover)); // idempotent

        var steps = JsonSerializer.Deserialize<List<OasChangeoverStepDto>>(changeover.Steps) ?? new();
        if (steps.Count == 0 || steps.Any(s => !s.Done))
        {
            return (false, "checklist_incomplete", null);
        }

        changeover.EndedAt = DateTimeOffset.UtcNow;
        changeover.DurationSec = (int)(changeover.EndedAt.Value - changeover.StartedAt).TotalSeconds;
        changeover.ValidatedBy = actorId;
        await _db.SaveChangesAsync();
        return (true, null, ToDto(changeover));
    }

    public async Task<IReadOnlyList<OasChangeoverDto>> GetAsync(int tenantId, Guid? postId, string? status)
    {
        var q = _db.Set<OasChangeover>().AsQueryable();
        if (postId is not null) q = q.Where(c => c.PostId == postId);
        if (string.Equals(status, "open", StringComparison.OrdinalIgnoreCase)) q = q.Where(c => c.EndedAt == null);
        else if (string.Equals(status, "closed", StringComparison.OrdinalIgnoreCase)) q = q.Where(c => c.EndedAt != null);

        var rows = await q.OrderByDescending(c => c.StartedAt).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    private static OasChangeoverDto ToDto(OasChangeover c) => new()
    {
        Id = c.Id, PostId = c.PostId, FromProductId = c.FromProductId, ToProductId = c.ToProductId,
        StartedAt = c.StartedAt, EndedAt = c.EndedAt,
        Steps = JsonSerializer.Deserialize<List<OasChangeoverStepDto>>(c.Steps) ?? new(),
    };
}
