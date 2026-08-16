using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.PlanningProfiles.DTOs;
using MyApi.Modules.PlanningProfiles.Models;

namespace MyApi.Modules.PlanningProfiles.Services
{
    public class PlanningProfileService : IPlanningProfileService
    {
        // Hard caps for JSONB payloads to keep storage/parse cost bounded.
        private const int MaxSettingsJsonBytes = 32 * 1024;   // 32 KB
        private const int MaxVisibleUsers = 500;
        private const int MaxRequiredSkills = 200;

        private readonly ApplicationDbContext _db;
        private readonly ILogger<PlanningProfileService> _logger;

        public PlanningProfileService(ApplicationDbContext db, ILogger<PlanningProfileService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// A shared profile is visible to a user when either:
        ///   (a) VisibleUserIds is empty (interpreted as "all users in tenant"), or
        ///   (b) VisibleUserIds contains the user's id.
        /// Owners always see their own profiles regardless of the visibility list.
        /// </summary>
        private static bool IsVisibleTo(PlanningProfile p, string currentUserId)
        {
            if (p.OwnerUserId == currentUserId) return true;
            if (!p.IsShared) return false;
            List<string> visible;
            try { visible = JsonSerializer.Deserialize<List<string>>(p.VisibleUserIdsJson) ?? new(); }
            catch { visible = new(); }
            return visible.Count == 0 || visible.Contains(currentUserId);
        }

        public async Task<List<PlanningProfileDto>> ListAsync(string currentUserId)
        {
            // Prefilter in SQL to owned OR shared; final visible_user_ids check runs in memory
            // (JSONB containment would require raw SQL / EF.Functions and the row count per
            // tenant is small — profiles are user-authored dashboards, not high-volume data).
            var candidates = await _db.Set<PlanningProfile>()
                .Where(p => p.DeletedAt == null && (p.OwnerUserId == currentUserId || p.IsShared))
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();
            return candidates.Where(p => IsVisibleTo(p, currentUserId)).Select(ToDto).ToList();
        }

        public async Task<PlanningProfileDto?> GetByIdAsync(int id, string currentUserId)
        {
            var p = await _db.Set<PlanningProfile>().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
            if (p == null) return null;
            if (!IsVisibleTo(p, currentUserId)) return null;
            return ToDto(p);
        }

        public async Task<PlanningProfileDto> CreateAsync(CreatePlanningProfileDto dto, string currentUserId)
        {
            ValidatePayload(dto.VisibleUserIds, dto.RequiredSkillIds, dto.Settings);

            var p = new PlanningProfile
            {
                OwnerUserId = currentUserId,
                Name = dto.Name,
                Description = dto.Description,
                Color = dto.Color,
                Icon = dto.Icon,
                IsShared = dto.IsShared,
                VisibleUserIdsJson = JsonSerializer.Serialize(dto.VisibleUserIds ?? new List<string>()),
                RequiredSkillIdsJson = dto.RequiredSkillIds != null ? JsonSerializer.Serialize(dto.RequiredSkillIds) : null,
                SettingsJson = JsonSerializer.Serialize(dto.Settings ?? new { }),
                CreatedBy = currentUserId,
                UpdatedBy = currentUserId,
            };
            _db.Set<PlanningProfile>().Add(p);
            await _db.SaveChangesAsync();
            return ToDto(p);
        }

        public async Task<PlanningProfileDto> UpdateAsync(int id, UpdatePlanningProfileDto dto, string currentUserId)
        {
            var p = await _db.Set<PlanningProfile>().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null)
                ?? throw new InvalidOperationException("Profile not found");
            if (p.OwnerUserId != currentUserId) throw new UnauthorizedAccessException("Not owner");

            ValidatePayload(dto.VisibleUserIds, dto.RequiredSkillIds, dto.Settings);

            if (dto.Name != null) p.Name = dto.Name;
            if (dto.Description != null) p.Description = dto.Description;
            if (dto.Color != null) p.Color = dto.Color;
            if (dto.Icon != null) p.Icon = dto.Icon;
            if (dto.IsShared.HasValue) p.IsShared = dto.IsShared.Value;
            if (dto.VisibleUserIds != null) p.VisibleUserIdsJson = JsonSerializer.Serialize(dto.VisibleUserIds);
            if (dto.RequiredSkillIds != null) p.RequiredSkillIdsJson = JsonSerializer.Serialize(dto.RequiredSkillIds);
            if (dto.Settings != null) p.SettingsJson = JsonSerializer.Serialize(dto.Settings);
            p.UpdatedAt = DateTime.UtcNow;
            p.UpdatedBy = currentUserId;
            await _db.SaveChangesAsync();
            return ToDto(p);
        }

        public async Task DeleteAsync(int id, string currentUserId)
        {
            var p = await _db.Set<PlanningProfile>().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null)
                ?? throw new InvalidOperationException("Profile not found");
            if (p.OwnerUserId != currentUserId) throw new UnauthorizedAccessException("Not owner");
            p.DeletedAt = DateTime.UtcNow;
            p.DeletedBy = currentUserId;

            // Clear every dangling "active profile" pointer to the deleted profile — otherwise the
            // planning board resolves to a deleted profile and renders nothing for those users.
            var pointers = await _db.Set<UserActivePlanningProfile>()
                .Where(x => x.ProfileId == id)
                .ToListAsync();
            if (pointers.Count > 0) _db.Set<UserActivePlanningProfile>().RemoveRange(pointers);

            await _db.SaveChangesAsync();
        }

        public async Task<PlanningProfileDto?> GetActiveAsync(string currentUserId)
        {
            var active = await _db.Set<UserActivePlanningProfile>()
                .FirstOrDefaultAsync(x => x.UserId == currentUserId);

            if (active != null)
            {
                var p = await _db.Set<PlanningProfile>()
                    .FirstOrDefaultAsync(x => x.Id == active.ProfileId && x.DeletedAt == null);
                // Do not leak a profile the user is no longer allowed to see (e.g. share revoked
                // after they set it active).
                if (p != null && IsVisibleTo(p, currentUserId)) return ToDto(p);

                // Pointer is stale (profile deleted or un-shared): drop it and fall through to the
                // default profile so the planning board still loads.
                _db.Set<UserActivePlanningProfile>().Remove(active);
                await _db.SaveChangesAsync();
            }

            // Prefer one of the user's own profiles; if they own none, fall back to any profile
            // shared with them, so the planning board is never left without a configuration.
            var candidates = await _db.Set<PlanningProfile>()
                .Where(p => p.DeletedAt == null && (p.OwnerUserId == currentUserId || p.IsShared))
                .OrderBy(p => p.Id)
                .ToListAsync();

            var fallback = candidates.FirstOrDefault(p => p.OwnerUserId == currentUserId)
                ?? candidates.FirstOrDefault(p => IsVisibleTo(p, currentUserId));
            return fallback != null ? ToDto(fallback) : null;

        }


        public async Task SetActiveAsync(int profileId, string currentUserId)
        {
            var p = await _db.Set<PlanningProfile>().FirstOrDefaultAsync(x => x.Id == profileId && x.DeletedAt == null)
                ?? throw new InvalidOperationException("Profile not found");
            if (!IsVisibleTo(p, currentUserId)) throw new UnauthorizedAccessException("Profile not visible to user");

            // Retry once on concurrent insert: two calls can both read `existing == null`
            // and race on the PK (user_id, tenant_id). The retry converts the loser into an
            // update against the row the winner just wrote.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var existing = await _db.Set<UserActivePlanningProfile>()
                    .FirstOrDefaultAsync(x => x.UserId == currentUserId);
                if (existing == null)
                {
                    _db.Set<UserActivePlanningProfile>().Add(new UserActivePlanningProfile
                    {
                        UserId = currentUserId,
                        ProfileId = profileId,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    existing.ProfileId = profileId;
                    existing.UpdatedAt = DateTime.UtcNow;
                }

                try
                {
                    await _db.SaveChangesAsync();
                    return;
                }
                catch (DbUpdateException) when (attempt == 0)
                {
                    // Detach the failed insert so the next attempt can update the winning row.
                    foreach (var e in _db.ChangeTracker.Entries<UserActivePlanningProfile>().ToList())
                        e.State = EntityState.Detached;
                }
            }
        }

        private static void ValidatePayload(List<string>? visibleUserIds, List<string>? requiredSkillIds, object? settings)
        {
            if (visibleUserIds != null && visibleUserIds.Count > MaxVisibleUsers)
                throw new ArgumentException($"VisibleUserIds exceeds max ({MaxVisibleUsers}).");
            if (requiredSkillIds != null && requiredSkillIds.Count > MaxRequiredSkills)
                throw new ArgumentException($"RequiredSkillIds exceeds max ({MaxRequiredSkills}).");
            if (settings != null)
            {
                var json = JsonSerializer.Serialize(settings);
                if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxSettingsJsonBytes)
                    throw new ArgumentException($"Settings JSON exceeds max size ({MaxSettingsJsonBytes} bytes).");
            }
        }

        private PlanningProfileDto ToDto(PlanningProfile p)
        {
            List<string> visible;
            try { visible = JsonSerializer.Deserialize<List<string>>(p.VisibleUserIdsJson) ?? new(); }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Corrupt visible_user_ids JSON on planning_profile {Id}", p.Id);
                visible = new();
            }

            List<string>? skills = null;
            if (!string.IsNullOrEmpty(p.RequiredSkillIdsJson))
            {
                try { skills = JsonSerializer.Deserialize<List<string>>(p.RequiredSkillIdsJson); }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Corrupt required_skill_ids JSON on planning_profile {Id}", p.Id);
                    skills = null;
                }
            }

            object settings;
            try { settings = JsonSerializer.Deserialize<JsonElement>(p.SettingsJson); }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Corrupt settings JSON on planning_profile {Id}", p.Id);
                settings = new { };
            }

            return new PlanningProfileDto
            {
                Id = p.Id,
                TenantId = p.TenantId,
                OwnerUserId = p.OwnerUserId,
                Name = p.Name,
                Description = p.Description,
                Color = p.Color,
                Icon = p.Icon,
                IsShared = p.IsShared,
                VisibleUserIds = visible,
                RequiredSkillIds = skills,
                Settings = settings,
                CreatedAt = p.CreatedAt.ToString("o"),
                UpdatedAt = p.UpdatedAt.ToString("o"),
            };
        }
    }
}
