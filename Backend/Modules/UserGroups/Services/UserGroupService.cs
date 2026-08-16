using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.UserGroups.DTOs;
using MyApi.Modules.UserGroups.Models;

namespace MyApi.Modules.UserGroups.Services
{
    public class UserGroupService : IUserGroupService
    {
        private readonly ApplicationDbContext _context;

        public UserGroupService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserGroupDto>> GetAllAsync()
        {
            return await _context.Set<UserGroup>()
                .AsNoTracking()
                .Where(g => !g.IsDeleted)
                .Select(g => new UserGroupDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    IsActive = g.IsActive,
                    MemberCount = g.Members.Count(m => m.IsActive),
                    CreatedAt = g.CreatedAt,
                    UpdatedAt = g.UpdatedAt
                })
                .OrderBy(g => g.Name)
                .ToListAsync();
        }

        public async Task<UserGroupDto?> GetByIdAsync(int id)
        {
            return await _context.Set<UserGroup>()
                .AsNoTracking()
                .Where(g => g.Id == id && !g.IsDeleted)
                .Select(g => new UserGroupDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    IsActive = g.IsActive,
                    MemberCount = g.Members.Count(m => m.IsActive),
                    CreatedAt = g.CreatedAt,
                    UpdatedAt = g.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<UserGroupDto> CreateAsync(CreateUserGroupRequest request, string createdBy)
        {
            var group = new UserGroup
            {
                Name = request.Name,
                Description = request.Description,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsDeleted = false
            };

            _context.Set<UserGroup>().Add(group);
            await _context.SaveChangesAsync();

            return new UserGroupDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                IsActive = group.IsActive,
                MemberCount = 0,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }

        public async Task<UserGroupDto> UpdateAsync(int id, UpdateUserGroupRequest request, string modifiedBy)
        {
            var group = await _context.Set<UserGroup>()
                .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

            if (group == null)
                throw new ArgumentException("User group not found");

            group.Name = request.Name;
            group.Description = request.Description;
            group.IsActive = request.IsActive;
            group.ModifiedBy = modifiedBy;
            group.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var memberCount = await _context.Set<UserGroupMember>()
                .CountAsync(m => m.GroupId == id && m.IsActive);

            return new UserGroupDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                IsActive = group.IsActive,
                MemberCount = memberCount,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var group = await _context.Set<UserGroup>()
                .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

            if (group == null)
                return false;

            group.IsDeleted = true;
            group.IsActive = false;
            group.UpdatedAt = DateTime.UtcNow;

            var members = await _context.Set<UserGroupMember>()
                .Where(m => m.GroupId == id)
                .ToListAsync();
            foreach (var m in members) m.IsActive = false;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(string name, int? excludeId = null)
        {
            var query = _context.Set<UserGroup>()
                .Where(g => !g.IsDeleted && g.Name.ToLower() == name.ToLower());
            if (excludeId.HasValue)
                query = query.Where(g => g.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<IEnumerable<UserGroupMemberDto>> GetMembersAsync(int groupId)
        {
            return await (
                from m in _context.Set<UserGroupMember>().AsNoTracking()
                join u in _context.Users.AsNoTracking() on m.UserId equals u.Id
                where m.GroupId == groupId && m.IsActive && !u.IsDeleted
                orderby u.FirstName, u.LastName
                select new UserGroupMemberDto
                {
                    UserId = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    AssignedAt = m.AssignedAt
                }
            ).ToListAsync();
        }

        public async Task<int> AssignUsersAsync(int groupId, IEnumerable<int> userIds, string assignedBy)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return 0;

            var existing = await _context.Set<UserGroupMember>()
                .Where(m => m.GroupId == groupId && ids.Contains(m.UserId))
                .ToListAsync();

            var now = DateTime.UtcNow;
            var added = 0;

            foreach (var uid in ids)
            {
                var row = existing.FirstOrDefault(e => e.UserId == uid);
                if (row == null)
                {
                    _context.Set<UserGroupMember>().Add(new UserGroupMember
                    {
                        GroupId = groupId,
                        UserId = uid,
                        AssignedBy = assignedBy,
                        AssignedAt = now,
                        IsActive = true
                    });
                    added++;
                }
                else if (!row.IsActive)
                {
                    row.IsActive = true;
                    row.AssignedAt = now;
                    row.AssignedBy = assignedBy;
                    added++;
                }
            }

            await _context.SaveChangesAsync();
            return added;
        }

        public async Task<bool> RemoveMemberAsync(int groupId, int userId)
        {
            var row = await _context.Set<UserGroupMember>()
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
            if (row == null) return false;
            row.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<UserGroupDto>> GetUserGroupsAsync(int userId)
        {
            return await (
                from m in _context.Set<UserGroupMember>().AsNoTracking()
                join g in _context.Set<UserGroup>().AsNoTracking() on m.GroupId equals g.Id
                where m.UserId == userId && m.IsActive && !g.IsDeleted && g.IsActive
                orderby g.Name
                select new UserGroupDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    IsActive = g.IsActive,
                    MemberCount = g.Members.Count(mm => mm.IsActive),
                    CreatedAt = g.CreatedAt,
                    UpdatedAt = g.UpdatedAt
                }
            ).ToListAsync();
        }
    }
}
