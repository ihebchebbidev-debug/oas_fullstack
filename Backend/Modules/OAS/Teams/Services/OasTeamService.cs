using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Teams.DTOs;
using MyApi.Modules.OAS.Teams.Models;

namespace MyApi.Modules.OAS.Teams.Services;

public interface IOasTeamService
{
    Task<IReadOnlyList<OasTeamDto>> GetAllAsync(int tenantId);
    Task<OasTeamDto> CreateAsync(int tenantId, OasTeamRequestDto request);
    Task<bool> SetMembersAsync(int tenantId, Guid teamId, OasTeamMembersRequestDto request);
}

public class OasTeamService : IOasTeamService
{
    private readonly OasDbContext _db;
    public OasTeamService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasTeamDto>> GetAllAsync(int tenantId)
    {
        var teams = await _db.Set<OasTeam>().OrderBy(t => t.Name).ToListAsync();
        var members = await _db.Set<OasTeamMember>().Where(m => m.ValidTo == null).ToListAsync();
        return teams.Select(t => ToDto(t, members.Where(m => m.TeamId == t.Id).Select(m => m.UserId).ToList())).ToList();
    }

    public async Task<OasTeamDto> CreateAsync(int tenantId, OasTeamRequestDto request)
    {
        var team = new OasTeam { TenantId = tenantId, SiteId = request.SiteId, Code = request.Code, Name = request.Name, LeadUserId = request.LeadUserId };
        _db.Set<OasTeam>().Add(team);
        await _db.SaveChangesAsync();
        return ToDto(team, new List<Guid>());
    }

    public async Task<bool> SetMembersAsync(int tenantId, Guid teamId, OasTeamMembersRequestDto request)
    {
        var team = await _db.Set<OasTeam>().FindAsync(teamId);
        if (team is null) return false;

        var currentMembers = await _db.Set<OasTeamMember>().Where(m => m.TeamId == teamId && m.ValidTo == null).ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var member in currentMembers.Where(m => !request.MemberIds.Contains(m.UserId)))
        {
            member.ValidTo = today;
        }

        var existingUserIds = currentMembers.Select(m => m.UserId).ToHashSet();
        foreach (var userId in request.MemberIds.Where(id => !existingUserIds.Contains(id)))
        {
            _db.Set<OasTeamMember>().Add(new OasTeamMember { TenantId = tenantId, TeamId = teamId, UserId = userId, ValidFrom = today });
        }

        await _db.SaveChangesAsync();
        return true;
    }

    private static OasTeamDto ToDto(OasTeam t, List<Guid> memberIds) => new()
    {
        Id = t.Id, SiteId = t.SiteId, Code = t.Code, Name = t.Name, LeadUserId = t.LeadUserId, MemberIds = memberIds,
    };
}
