using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.PostStates.DTOs;
using MyApi.Modules.OAS.PostStates.Models;

namespace MyApi.Modules.OAS.PostStates.Services;

public interface IOasPostStateService
{
    Task<IReadOnlyList<OasPostStateDto>> GetLiveAsync(int tenantId);
    Task<IReadOnlyList<OasPostStateHistoryDto>> GetHistoryAsync(int tenantId, Guid postId);
}

public class OasPostStateService : IOasPostStateService
{
    private readonly OasDbContext _db;
    public OasPostStateService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasPostStateDto>> GetLiveAsync(int tenantId)
    {
        var rows = await _db.Set<OasPostState>().ToListAsync();
        return rows.Select(s => new OasPostStateDto
        {
            PostId = s.PostId, State = s.State.ToString(), Since = s.Since,
            ActiveEventId = s.ActiveEventId, CurrentUserId = s.CurrentUserId, CurrentProductId = s.CurrentProductId,
        }).ToList();
    }

    public async Task<IReadOnlyList<OasPostStateHistoryDto>> GetHistoryAsync(int tenantId, Guid postId)
    {
        var rows = await _db.Set<OasPostStateHistory>().Where(h => h.PostId == postId).OrderByDescending(h => h.StartedAt).ToListAsync();
        return rows.Select(h => new OasPostStateHistoryDto { State = h.State.ToString(), StartedAt = h.StartedAt, EndedAt = h.EndedAt, DurationSec = h.DurationSec }).ToList();
    }
}
