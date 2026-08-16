using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Assignments.DTOs;
using MyApi.Modules.OAS.Assignments.Models;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Hierarchy.Models;
using MyApi.Modules.OAS.ShopFloorAuth.Models;

namespace MyApi.Modules.OAS.Assignments.Services;

public interface IOasAssignmentService
{
    Task<IReadOnlyList<OasAssignmentDto>> GetAsync(int tenantId, Guid shiftTemplateId, DateOnly workDate, bool onlyPublished);
    Task<OasAssignmentDto> UpsertAsync(int tenantId, Guid actorId, Guid postId, OasAssignmentRequestDto request);
    Task<bool> DeleteAsync(int tenantId, Guid postId, Guid shiftTemplateId, DateOnly workDate);
    Task<int> AutoFillAsync(int tenantId, Guid actorId, Guid shiftTemplateId, DateOnly workDate);
    Task<int> ClearAllAsync(int tenantId, Guid shiftTemplateId, DateOnly workDate);
    Task<int> PublishAsync(int tenantId, Guid shiftTemplateId, DateOnly workDate, Guid? postId);
    Task<OasAssignmentCountsDto> GetCountsAsync(int tenantId, Guid shiftTemplateId, DateOnly workDate);
    Task<IReadOnlyList<OasRosterEntryDto>> GetRosterAsync(int tenantId);

    Task<bool> SetPresenceAsync(int tenantId, Guid userId, OasPresenceRequestDto request);
    Task<bool> ConfirmPresenceAsync(int tenantId, Guid userId, OasPresenceConfirmRequestDto request);
    Task<IReadOnlyList<OasPresenceDto>> GetPresenceAsync(int tenantId, Guid shiftTemplateId, DateOnly workDate);
}

/// <summary>v12/v15: assignments and presence are keyed by oas_users.Id (uuid), never by display-name string — the old client matched operators by name, which collides and doesn't survive a rename.</summary>
public class OasAssignmentService : IOasAssignmentService
{
    private readonly OasDbContext _db;
    public OasAssignmentService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasAssignmentDto>> GetAsync(int tenantId, Guid shiftTemplateId, DateOnly workDate, bool onlyPublished)
    {
        var q = _db.Set<OasAssignment>().Where(a => a.ShiftTemplateId == shiftTemplateId && a.WorkDate == workDate);
        if (onlyPublished) q = q.Where(a => a.PublishedAt != null);
        var rows = await q.ToListAsync();

        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var names = await _db.Set<OasUser>().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        return rows.Select(a => ToDto(a, names.GetValueOrDefault(a.UserId))).ToList();
    }

    public async Task<OasAssignmentDto> UpsertAsync(int tenantId, Guid actorId, Guid postId, OasAssignmentRequestDto request)
    {
        var existing = await _db.Set<OasAssignment>().FirstOrDefaultAsync(a =>
            a.PostId == postId && a.ShiftTemplateId == request.ShiftTemplateId && a.WorkDate == request.WorkDate);

        if (existing is null)
        {
            existing = new OasAssignment
            {
                TenantId = tenantId, PostId = postId, ShiftTemplateId = request.ShiftTemplateId, WorkDate = request.WorkDate,
                UserId = request.UserId, ProductionOrderId = request.ProductionOrderId, Note = request.Note, AssignedBy = actorId,
            };
            _db.Set<OasAssignment>().Add(existing);
        }
        else
        {
            existing.UserId = request.UserId;
            existing.ProductionOrderId = request.ProductionOrderId;
            existing.Note = request.Note;
            existing.AssignedBy = actorId;
            // v15: editing THIS post un-publishes only THIS post's row, never the whole board.
            existing.PublishedAt = null;
        }

        await _db.SaveChangesAsync();
        var name = await _db.Set<OasUser>().Where(u => u.Id == existing.UserId).Select(u => u.DisplayName).FirstOrDefaultAsync();
        return ToDto(existing, name);
    }

    public async Task<bool> DeleteAsync(int tenantId, Guid postId, Guid shiftTemplateId, DateOnly workDate)
    {
        var assignment = await _db.Set<OasAssignment>().FirstOrDefaultAsync(a => a.PostId == postId && a.ShiftTemplateId == shiftTemplateId && a.WorkDate == workDate);
        if (assignment is null) return false;
        _db.Set<OasAssignment>().Remove(assignment);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> AutoFillAsync(int tenantId, Guid actorId, Guid shiftTemplateId, DateOnly workDate)
    {
        var assignedPostIds = await _db.Set<OasAssignment>()
            .Where(a => a.ShiftTemplateId == shiftTemplateId && a.WorkDate == workDate)
            .Select(a => a.PostId).ToListAsync();
        var emptyPosts = await _db.Set<OasPost>().Where(p => p.IsActive && !assignedPostIds.Contains(p.Id)).OrderBy(p => p.SortOrder).ToListAsync();

        var alreadyAssignedUserIds = await _db.Set<OasAssignment>()
            .Where(a => a.ShiftTemplateId == shiftTemplateId && a.WorkDate == workDate)
            .Select(a => a.UserId).ToListAsync();
        var availableOperators = await _db.Set<OasUser>()
            .Where(u => u.IsActive && u.Role == OasAppRole.@operator && !alreadyAssignedUserIds.Contains(u.Id))
            .OrderBy(u => u.DisplayName).ToListAsync();

        var filled = 0;
        foreach (var (post, operatorUser) in emptyPosts.Zip(availableOperators))
        {
            _db.Set<OasAssignment>().Add(new OasAssignment
            {
                TenantId = tenantId, PostId = post.Id, UserId = operatorUser.Id, ShiftTemplateId = shiftTemplateId, WorkDate = workDate, AssignedBy = actorId,
            });
            filled++;
        }

        await _db.SaveChangesAsync();
        return filled;
    }

    public async Task<int> ClearAllAsync(int tenantId, Guid shiftTemplateId, DateOnly workDate)
    {
        var rows = await _db.Set<OasAssignment>().Where(a => a.ShiftTemplateId == shiftTemplateId && a.WorkDate == workDate).ToListAsync();
        _db.Set<OasAssignment>().RemoveRange(rows);
        await _db.SaveChangesAsync();
        return rows.Count;
    }

    public async Task<int> PublishAsync(int tenantId, Guid shiftTemplateId, DateOnly workDate, Guid? postId)
    {
        var q = _db.Set<OasAssignment>().Where(a => a.ShiftTemplateId == shiftTemplateId && a.WorkDate == workDate);
        if (postId is not null) q = q.Where(a => a.PostId == postId);
        var rows = await q.ToListAsync();
        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows) row.PublishedAt = now;
        await _db.SaveChangesAsync();
        return rows.Count;
    }

    public async Task<OasAssignmentCountsDto> GetCountsAsync(int tenantId, Guid shiftTemplateId, DateOnly workDate)
    {
        var totalPosts = await _db.Set<OasPost>().CountAsync(p => p.IsActive);
        var rows = await _db.Set<OasAssignment>().Where(a => a.ShiftTemplateId == shiftTemplateId && a.WorkDate == workDate).ToListAsync();
        return new OasAssignmentCountsDto { TotalPosts = totalPosts, Assigned = rows.Count, Published = rows.Count(r => r.PublishedAt != null) };
    }

    public async Task<IReadOnlyList<OasRosterEntryDto>> GetRosterAsync(int tenantId)
    {
        var rows = await _db.Set<OasUser>().Where(u => u.Role == OasAppRole.@operator).OrderBy(u => u.DisplayName).ToListAsync();
        return rows.Select(u => new OasRosterEntryDto { UserId = u.Id, DisplayName = u.DisplayName, IsActive = u.IsActive }).ToList();
    }

    public async Task<bool> SetPresenceAsync(int tenantId, Guid userId, OasPresenceRequestDto request)
    {
        var entry = await _db.Set<OasPresenceEntry>().FirstOrDefaultAsync(p => p.UserId == userId && p.WorkDate == request.WorkDate && p.ShiftTemplateId == request.ShiftTemplateId);
        if (entry is null)
        {
            entry = new OasPresenceEntry { TenantId = tenantId, UserId = userId, WorkDate = request.WorkDate, ShiftTemplateId = request.ShiftTemplateId };
            _db.Set<OasPresenceEntry>().Add(entry);
        }
        entry.Status = request.Status;
        entry.Reason = request.Reason;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>v10 confirmed fact: only mobile calls this (self-confirm), distinct from the console's PUT /presence/{operatorId}.</summary>
    public async Task<bool> ConfirmPresenceAsync(int tenantId, Guid userId, OasPresenceConfirmRequestDto request)
    {
        var entry = await _db.Set<OasPresenceEntry>().FirstOrDefaultAsync(p => p.UserId == userId && p.WorkDate == request.WorkDate && p.ShiftTemplateId == request.ShiftTemplateId);
        if (entry is null)
        {
            entry = new OasPresenceEntry { TenantId = tenantId, UserId = userId, WorkDate = request.WorkDate, ShiftTemplateId = request.ShiftTemplateId };
            _db.Set<OasPresenceEntry>().Add(entry);
        }
        entry.Status = "confirmed";
        entry.ConfirmedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<OasPresenceDto>> GetPresenceAsync(int tenantId, Guid shiftTemplateId, DateOnly workDate)
    {
        var rows = await _db.Set<OasPresenceEntry>().Where(p => p.ShiftTemplateId == shiftTemplateId && p.WorkDate == workDate).ToListAsync();
        return rows.Select(p => new OasPresenceDto
        {
            UserId = p.UserId, WorkDate = p.WorkDate, ShiftTemplateId = p.ShiftTemplateId,
            Status = p.Status, ConfirmedAt = p.ConfirmedAt, Reason = p.Reason,
        }).ToList();
    }

    private static OasAssignmentDto ToDto(OasAssignment a, string? userName) => new()
    {
        PostId = a.PostId, UserId = a.UserId, UserDisplayName = userName, ShiftTemplateId = a.ShiftTemplateId,
        WorkDate = a.WorkDate, ProductionOrderId = a.ProductionOrderId, Note = a.Note, Published = a.PublishedAt != null,
    };
}
