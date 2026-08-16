using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyApi.Modules.HR.DTOs;
using MyApi.Modules.HR.Models;

namespace MyApi.Modules.HR.Services
{
    public partial class HrService
    {
        // =========================================================================
        // Helpers
        // =========================================================================
        private async Task<Dictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds)
        {
            var ids = userIds.Where(i => i > 0).Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, string>();
            var rows = await _db.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync();
            return rows.ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(($"{u.FirstName} {u.LastName}").Trim())
                    ? (u.Email ?? $"#{u.Id}") : ($"{u.FirstName} {u.LastName}").Trim());
        }

        private async Task LogPerfRecruitAsync(int? userId, string eventType, string description,
            object? payload, int actorUserId)
        {
            try
            {
                _db.HrAuditLogs.Add(new HrAuditLog
                {
                    UserId = userId ?? 0,
                    EventType = eventType,
                    Description = description,
                    Payload = payload != null ? JsonSerializer.Serialize(payload) : null,
                    ActorUserId = actorUserId == 0 ? null : actorUserId,
                });
                await _db.SaveChangesAsync();
            }
            catch { /* best-effort */ }
        }

        // =========================================================================
        // PERFORMANCE: GOALS
        // =========================================================================
        public async Task<List<HrGoalDto>> GetGoalsAsync(int? userId, int? cycleId, string? status)
        {
            var q = _db.HrGoals.Where(g => !g.IsDeleted);
            if (userId.HasValue) q = q.Where(g => g.UserId == userId.Value);
            if (cycleId.HasValue) q = q.Where(g => g.CycleId == cycleId.Value);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(g => g.Status == status);
            var rows = await q.OrderByDescending(g => g.CreatedAt).ToListAsync();

            var names = await GetUserNamesAsync(rows.Select(r => r.UserId));
            var cycles = await _db.HrReviewCycles
                .Where(c => rows.Select(r => r.CycleId).Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            return rows.Select(g => new HrGoalDto
            {
                Id = g.Id, UserId = g.UserId,
                UserName = names.TryGetValue(g.UserId, out var n) ? n : $"#{g.UserId}",
                CycleId = g.CycleId,
                CycleName = g.CycleId.HasValue && cycles.TryGetValue(g.CycleId.Value, out var cn) ? cn : null,
                Title = g.Title, Description = g.Description, Category = g.Category,
                Weight = g.Weight, TargetValue = g.TargetValue,
                Progress = g.Progress, Status = g.Status,
                DueDate = g.DueDate, Score = g.Score, CreatedAt = g.CreatedAt,
            }).ToList();
        }

        public async Task<HrGoalDto> CreateGoalAsync(UpsertHrGoalDto dto, int actorUserId)
        {
            var entity = new HrGoal
            {
                UserId = dto.UserId,
                CycleId = dto.CycleId,
                Title = dto.Title?.Trim() ?? string.Empty,
                Description = dto.Description,
                Category = dto.Category ?? "smart",
                Weight = Math.Max(0, Math.Min(100, dto.Weight ?? 0)),
                TargetValue = dto.TargetValue,
                Progress = Math.Max(0, Math.Min(100, dto.Progress ?? 0)),
                Status = dto.Status ?? "not_started",
                DueDate = dto.DueDate.HasValue ? DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc) : (DateTime?)null,
                Score = dto.Score,
                CreatedBy = actorUserId == 0 ? null : actorUserId,
            };
            _db.HrGoals.Add(entity);
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(entity.UserId, "goal_created", $"Goal '{entity.Title}' created", new { entity.Id, entity.Title }, actorUserId);
            return (await GetGoalsAsync(entity.UserId, null, null)).First(g => g.Id == entity.Id);
        }

        public async Task<HrGoalDto> UpdateGoalAsync(int id, UpsertHrGoalDto dto, int actorUserId)
        {
            var entity = await _db.HrGoals.FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted)
                         ?? throw new KeyNotFoundException("hr.goal_not_found");
            if (!string.IsNullOrWhiteSpace(dto.Title)) entity.Title = dto.Title.Trim();
            if (dto.Description != null) entity.Description = dto.Description;
            if (dto.Category != null) entity.Category = dto.Category;
            if (dto.Weight.HasValue) entity.Weight = Math.Max(0, Math.Min(100, dto.Weight.Value));
            if (dto.TargetValue != null) entity.TargetValue = dto.TargetValue;
            if (dto.Progress.HasValue) entity.Progress = Math.Max(0, Math.Min(100, dto.Progress.Value));
            if (dto.Status != null) entity.Status = dto.Status;
            if (dto.DueDate.HasValue) entity.DueDate = DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc);
            if (dto.Score.HasValue) entity.Score = dto.Score;
            if (dto.CycleId.HasValue) entity.CycleId = dto.CycleId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(entity.UserId, "goal_updated", $"Goal '{entity.Title}' updated", new { entity.Id, entity.Status, entity.Progress }, actorUserId);
            return (await GetGoalsAsync(entity.UserId, null, null)).First(g => g.Id == entity.Id);
        }

        public async Task DeleteGoalAsync(int id, int actorUserId)
        {
            var entity = await _db.HrGoals.FirstOrDefaultAsync(g => g.Id == id);
            if (entity == null) return;
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(entity.UserId, "goal_deleted", $"Goal '{entity.Title}' deleted", new { entity.Id }, actorUserId);
        }

        // =========================================================================
        // PERFORMANCE: REVIEW CYCLES
        // =========================================================================
        public async Task<List<HrReviewCycleDto>> GetReviewCyclesAsync()
        {
            var rows = await _db.HrReviewCycles.Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.PeriodStart).ToListAsync();
            var ids = rows.Select(r => r.Id).ToList();
            var stats = await _db.HrPerformanceReviews
                .Where(r => ids.Contains(r.CycleId) && !r.IsDeleted)
                .GroupBy(r => r.CycleId)
                .Select(g => new { CycleId = g.Key, Total = g.Count(), Completed = g.Count(x => x.Status == "completed" || x.Status == "acknowledged") })
                .ToListAsync();
            var statsById = stats.ToDictionary(s => s.CycleId);
            return rows.Select(c => new HrReviewCycleDto
            {
                Id = c.Id, Name = c.Name, Description = c.Description, Frequency = c.Frequency,
                PeriodStart = c.PeriodStart, PeriodEnd = c.PeriodEnd, Status = c.Status,
                SelfAssessmentRequired = c.SelfAssessmentRequired,
                ReviewsCount = statsById.TryGetValue(c.Id, out var s) ? s.Total : 0,
                CompletedReviewsCount = statsById.TryGetValue(c.Id, out var s2) ? s2.Completed : 0,
            }).ToList();
        }

        public async Task<HrReviewCycleDto> CreateReviewCycleAsync(UpsertHrReviewCycleDto dto, int actorUserId)
        {
            var entity = new HrReviewCycle
            {
                Name = dto.Name?.Trim() ?? string.Empty,
                Description = dto.Description,
                Frequency = dto.Frequency ?? "annual",
                PeriodStart = dto.PeriodStart,
                PeriodEnd = dto.PeriodEnd,
                Status = dto.Status ?? "draft",
                SelfAssessmentRequired = dto.SelfAssessmentRequired ?? true,
                CreatedBy = actorUserId == 0 ? null : actorUserId,
            };
            _db.HrReviewCycles.Add(entity);
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "review_cycle_created", $"Cycle '{entity.Name}' created", new { entity.Id }, actorUserId);
            return (await GetReviewCyclesAsync()).First(c => c.Id == entity.Id);
        }

        public async Task<HrReviewCycleDto> UpdateReviewCycleAsync(int id, UpsertHrReviewCycleDto dto, int actorUserId)
        {
            var entity = await _db.HrReviewCycles.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted)
                ?? throw new KeyNotFoundException("hr.cycle_not_found");
            if (!string.IsNullOrWhiteSpace(dto.Name)) entity.Name = dto.Name.Trim();
            if (dto.Description != null) entity.Description = dto.Description;
            if (!string.IsNullOrWhiteSpace(dto.Frequency)) entity.Frequency = dto.Frequency;
            if (dto.PeriodStart != default) entity.PeriodStart = dto.PeriodStart;
            if (dto.PeriodEnd != default) entity.PeriodEnd = dto.PeriodEnd;
            if (!string.IsNullOrWhiteSpace(dto.Status)) entity.Status = dto.Status;
            if (dto.SelfAssessmentRequired.HasValue) entity.SelfAssessmentRequired = dto.SelfAssessmentRequired.Value;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "review_cycle_updated", $"Cycle '{entity.Name}' updated", new { entity.Id, entity.Status }, actorUserId);
            return (await GetReviewCyclesAsync()).First(c => c.Id == entity.Id);
        }

        public async Task DeleteReviewCycleAsync(int id, int actorUserId)
        {
            var entity = await _db.HrReviewCycles.FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null) return;
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "review_cycle_deleted", $"Cycle '{entity.Name}' deleted", new { entity.Id }, actorUserId);
        }

        // =========================================================================
        // PERFORMANCE: REVIEWS
        // =========================================================================
        private async Task<HrPerformanceReviewDto> MapReviewAsync(HrPerformanceReview r)
        {
            var ids = new List<int>();
            if (r.UserId > 0) ids.Add(r.UserId);
            if (r.ReviewerUserId.HasValue && r.ReviewerUserId.Value > 0) ids.Add(r.ReviewerUserId.Value);
            var names = await GetUserNamesAsync(ids);
            var cycle = await _db.HrReviewCycles.Where(c => c.Id == r.CycleId).Select(c => c.Name).FirstOrDefaultAsync();
            var goals = await GetGoalsAsync(r.UserId, r.CycleId, null);
            return new HrPerformanceReviewDto
            {
                Id = r.Id, UserId = r.UserId,
                UserName = names.TryGetValue(r.UserId, out var un) ? un : $"#{r.UserId}",
                CycleId = r.CycleId, CycleName = cycle,
                ReviewerUserId = r.ReviewerUserId,
                ReviewerName = r.ReviewerUserId.HasValue && names.TryGetValue(r.ReviewerUserId.Value, out var rn) ? rn : null,
                Status = r.Status,
                SelfAssessment = r.SelfAssessment,
                SelfAssessmentSubmittedAt = r.SelfAssessmentSubmittedAt,
                ManagerComments = r.ManagerComments,
                OverallScore = r.OverallScore, Rating = r.Rating,
                Strengths = r.Strengths, Improvements = r.Improvements,
                DevelopmentPlan = r.DevelopmentPlan,
                CompletedAt = r.CompletedAt, AcknowledgedAt = r.AcknowledgedAt,
                Goals = goals,
            };
        }

        public async Task<List<HrPerformanceReviewDto>> GetReviewsAsync(int? cycleId, int? userId, string? status)
        {
            var q = _db.HrPerformanceReviews.Where(r => !r.IsDeleted);
            if (cycleId.HasValue) q = q.Where(r => r.CycleId == cycleId.Value);
            if (userId.HasValue) q = q.Where(r => r.UserId == userId.Value);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
            var rows = await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
            if (rows.Count == 0) return new List<HrPerformanceReviewDto>();

            // Batch: all users referenced by reviews (reviewee + reviewer)
            var allUserIds = rows
                .SelectMany(r => r.ReviewerUserId.HasValue ? new[] { r.UserId, r.ReviewerUserId.Value } : new[] { r.UserId })
                .Distinct();
            var names = await GetUserNamesAsync(allUserIds);

            // Batch: cycle names for all cycles referenced by reviews
            var reviewCycleIds = rows.Select(r => r.CycleId).Distinct().ToList();
            var cycles = await _db.HrReviewCycles
                .Where(c => reviewCycleIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            // Batch: all goals for all reviewees (filtered per-review in-memory)
            var revieweeIds = rows.Select(r => r.UserId).Distinct().ToList();
            var allGoals = await _db.HrGoals
                .Where(g => !g.IsDeleted && revieweeIds.Contains(g.UserId))
                .ToListAsync();

            // Batch: user names for goal owners (may overlap with review users)
            var goalUserIds = allGoals.Select(g => g.UserId).Distinct().Except(names.Keys);
            var extraGoalNames = await GetUserNamesAsync(goalUserIds);
            var goalNames = names.Concat(extraGoalNames).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.First().Value);

            // Batch: cycle names for cycles referenced by goals (may add new ones)
            var goalCycleIds = allGoals.Where(g => g.CycleId.HasValue).Select(g => g.CycleId!.Value).Distinct().Except(cycles.Keys).ToList();
            if (goalCycleIds.Count > 0)
            {
                var extraCycles = await _db.HrReviewCycles
                    .Where(c => goalCycleIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Name);
                foreach (var kv in extraCycles) cycles[kv.Key] = kv.Value;
            }

            return rows.Select(r =>
            {
                var goals = allGoals
                    .Where(g => g.UserId == r.UserId && g.CycleId == r.CycleId)
                    .Select(g => new HrGoalDto
                    {
                        Id = g.Id, UserId = g.UserId,
                        UserName = goalNames.TryGetValue(g.UserId, out var gn) ? gn : $"#{g.UserId}",
                        CycleId = g.CycleId,
                        CycleName = g.CycleId.HasValue && cycles.TryGetValue(g.CycleId.Value, out var gcn) ? gcn : null,
                        Title = g.Title, Description = g.Description, Category = g.Category,
                        Weight = g.Weight, TargetValue = g.TargetValue,
                        Progress = g.Progress, Status = g.Status,
                        DueDate = g.DueDate, Score = g.Score, CreatedAt = g.CreatedAt,
                    }).ToList();

                return new HrPerformanceReviewDto
                {
                    Id = r.Id, UserId = r.UserId,
                    UserName = names.TryGetValue(r.UserId, out var un) ? un : $"#{r.UserId}",
                    CycleId = r.CycleId, CycleName = cycles.TryGetValue(r.CycleId, out var cn) ? cn : null,
                    ReviewerUserId = r.ReviewerUserId,
                    ReviewerName = r.ReviewerUserId.HasValue && names.TryGetValue(r.ReviewerUserId.Value, out var rn) ? rn : null,
                    Status = r.Status,
                    SelfAssessment = r.SelfAssessment,
                    SelfAssessmentSubmittedAt = r.SelfAssessmentSubmittedAt,
                    ManagerComments = r.ManagerComments,
                    OverallScore = r.OverallScore, Rating = r.Rating,
                    Strengths = r.Strengths, Improvements = r.Improvements,
                    DevelopmentPlan = r.DevelopmentPlan,
                    CompletedAt = r.CompletedAt, AcknowledgedAt = r.AcknowledgedAt,
                    Goals = goals,
                };
            }).ToList();
        }

        public async Task<HrPerformanceReviewDto> GetReviewAsync(int id)
        {
            var r = await _db.HrPerformanceReviews.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                ?? throw new KeyNotFoundException("hr.review_not_found");
            return await MapReviewAsync(r);
        }

        public async Task<HrPerformanceReviewDto> CreateReviewAsync(UpsertHrPerformanceReviewDto dto, int actorUserId)
        {
            var entity = new HrPerformanceReview
            {
                UserId = dto.UserId, CycleId = dto.CycleId,
                ReviewerUserId = dto.ReviewerUserId,
                Status = dto.Status ?? "pending",
                SelfAssessment = dto.SelfAssessment,
                ManagerComments = dto.ManagerComments,
                OverallScore = dto.OverallScore,
                Rating = dto.Rating,
                Strengths = dto.Strengths,
                Improvements = dto.Improvements,
                DevelopmentPlan = dto.DevelopmentPlan,
                CreatedBy = actorUserId == 0 ? null : actorUserId,
            };
            _db.HrPerformanceReviews.Add(entity);
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(entity.UserId, "review_created", "Review created", new { entity.Id, entity.CycleId }, actorUserId);
            return await GetReviewAsync(entity.Id);
        }

        public async Task<HrPerformanceReviewDto> UpdateReviewAsync(int id, UpsertHrPerformanceReviewDto dto, int actorUserId)
        {
            var entity = await _db.HrPerformanceReviews.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted)
                ?? throw new KeyNotFoundException("hr.review_not_found");
            if (dto.ReviewerUserId.HasValue) entity.ReviewerUserId = dto.ReviewerUserId;
            if (!string.IsNullOrWhiteSpace(dto.Status)) entity.Status = dto.Status;
            if (dto.SelfAssessment != null)
            {
                if (string.IsNullOrEmpty(entity.SelfAssessment) && entity.SelfAssessmentSubmittedAt == null)
                    entity.SelfAssessmentSubmittedAt = DateTime.UtcNow;
                entity.SelfAssessment = dto.SelfAssessment;
            }
            if (dto.ManagerComments != null) entity.ManagerComments = dto.ManagerComments;
            if (dto.OverallScore.HasValue) entity.OverallScore = dto.OverallScore;
            if (dto.Rating != null) entity.Rating = dto.Rating;
            if (dto.Strengths != null) entity.Strengths = dto.Strengths;
            if (dto.Improvements != null) entity.Improvements = dto.Improvements;
            if (dto.DevelopmentPlan != null) entity.DevelopmentPlan = dto.DevelopmentPlan;
            if (entity.Status == "completed" && entity.CompletedAt == null) entity.CompletedAt = DateTime.UtcNow;
            if (entity.Status == "acknowledged" && entity.AcknowledgedAt == null) entity.AcknowledgedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(entity.UserId, "review_updated", $"Review {entity.Status}", new { entity.Id, entity.Status }, actorUserId);
            return await GetReviewAsync(entity.Id);
        }

        public async Task DeleteReviewAsync(int id, int actorUserId)
        {
            var entity = await _db.HrPerformanceReviews.FirstOrDefaultAsync(r => r.Id == id);
            if (entity == null) return;
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(entity.UserId, "review_deleted", "Review deleted", new { entity.Id }, actorUserId);
        }

        // =========================================================================
        // RECRUITMENT: JOB OPENINGS
        // =========================================================================
        private async Task<HrJobOpeningDto> MapOpeningAsync(HrJobOpening o)
        {
            var dept = o.DepartmentId.HasValue
                ? await _db.HrDepartments.Where(d => d.Id == o.DepartmentId).Select(d => d.Name).FirstOrDefaultAsync()
                : null;
            var hmName = o.HiringManagerUserId.HasValue
                ? (await GetUserNamesAsync(new[] { o.HiringManagerUserId.Value })).GetValueOrDefault(o.HiringManagerUserId.Value)
                : null;
            var stats = await _db.HrApplicants.Where(a => a.OpeningId == o.Id && !a.IsDeleted)
                .GroupBy(a => 1)
                .Select(g => new { Total = g.Count(), Hired = g.Count(x => x.Stage == "hired") })
                .FirstOrDefaultAsync();
            return new HrJobOpeningDto
            {
                Id = o.Id, Title = o.Title, DepartmentId = o.DepartmentId, DepartmentName = dept,
                Location = o.Location, ContractType = o.ContractType, Seniority = o.Seniority,
                Description = o.Description, Requirements = o.Requirements,
                SalaryMin = o.SalaryMin, SalaryMax = o.SalaryMax, Currency = o.Currency,
                OpeningsCount = o.OpeningsCount, Status = o.Status,
                HiringManagerUserId = o.HiringManagerUserId, HiringManagerName = hmName,
                OpenedAt = o.OpenedAt, ClosedAt = o.ClosedAt,
                ApplicantsCount = stats?.Total ?? 0, HiredCount = stats?.Hired ?? 0,
                CreatedAt = o.CreatedAt,
            };
        }

        public async Task<List<HrJobOpeningDto>> GetJobOpeningsAsync(string? status)
        {
            var q = _db.HrJobOpenings.Where(o => !o.IsDeleted);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(o => o.Status == status);
            var rows = await q.OrderByDescending(o => o.CreatedAt).ToListAsync();
            if (rows.Count == 0) return new List<HrJobOpeningDto>();

            var deptIds = rows.Where(o => o.DepartmentId.HasValue).Select(o => o.DepartmentId!.Value).Distinct().ToList();
            var deptNames = deptIds.Count > 0
                ? await _db.HrDepartments.Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name)
                : new Dictionary<int, string>();

            var hmUserIds = rows.Where(o => o.HiringManagerUserId.HasValue).Select(o => o.HiringManagerUserId!.Value).Distinct();
            var hmNames = await GetUserNamesAsync(hmUserIds);

            var openingIds = rows.Select(o => o.Id).ToList();
            var applicantStats = await _db.HrApplicants
                .Where(a => openingIds.Contains(a.OpeningId) && !a.IsDeleted)
                .GroupBy(a => a.OpeningId)
                .Select(g => new { OpeningId = g.Key, Total = g.Count(), Hired = g.Count(x => x.Stage == "hired") })
                .ToDictionaryAsync(g => g.OpeningId);

            return rows.Select(o => new HrJobOpeningDto
            {
                Id = o.Id, Title = o.Title,
                DepartmentId = o.DepartmentId,
                DepartmentName = o.DepartmentId.HasValue && deptNames.TryGetValue(o.DepartmentId.Value, out var dn) ? dn : null,
                Location = o.Location, ContractType = o.ContractType, Seniority = o.Seniority,
                Description = o.Description, Requirements = o.Requirements,
                SalaryMin = o.SalaryMin, SalaryMax = o.SalaryMax, Currency = o.Currency,
                OpeningsCount = o.OpeningsCount, Status = o.Status,
                HiringManagerUserId = o.HiringManagerUserId,
                HiringManagerName = o.HiringManagerUserId.HasValue && hmNames.TryGetValue(o.HiringManagerUserId.Value, out var hmn) ? hmn : null,
                OpenedAt = o.OpenedAt, ClosedAt = o.ClosedAt,
                ApplicantsCount = applicantStats.TryGetValue(o.Id, out var s) ? s.Total : 0,
                HiredCount = applicantStats.TryGetValue(o.Id, out var s2) ? s2.Hired : 0,
                CreatedAt = o.CreatedAt,
            }).ToList();
        }

        public async Task<HrJobOpeningDto> GetJobOpeningAsync(int id)
        {
            var o = await _db.HrJobOpenings.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                ?? throw new KeyNotFoundException("hr.opening_not_found");
            return await MapOpeningAsync(o);
        }

        public async Task<HrJobOpeningDto> CreateJobOpeningAsync(UpsertHrJobOpeningDto dto, int actorUserId)
        {
            var entity = new HrJobOpening
            {
                Title = dto.Title?.Trim() ?? string.Empty,
                DepartmentId = dto.DepartmentId,
                Location = dto.Location,
                ContractType = dto.ContractType,
                Seniority = dto.Seniority,
                Description = dto.Description,
                Requirements = dto.Requirements,
                SalaryMin = dto.SalaryMin,
                SalaryMax = dto.SalaryMax,
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "TND" : dto.Currency,
                OpeningsCount = Math.Max(1, dto.OpeningsCount ?? 1),
                Status = dto.Status ?? "draft",
                HiringManagerUserId = dto.HiringManagerUserId,
                OpenedAt = (dto.Status == "open") ? DateTime.UtcNow : null,
                CreatedBy = actorUserId == 0 ? null : actorUserId,
            };
            _db.HrJobOpenings.Add(entity);
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "job_opening_created", $"Opening '{entity.Title}' created", new { entity.Id }, actorUserId);
            return await MapOpeningAsync(entity);
        }

        public async Task<HrJobOpeningDto> UpdateJobOpeningAsync(int id, UpsertHrJobOpeningDto dto, int actorUserId)
        {
            var entity = await _db.HrJobOpenings.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted)
                ?? throw new KeyNotFoundException("hr.opening_not_found");
            if (!string.IsNullOrWhiteSpace(dto.Title)) entity.Title = dto.Title.Trim();
            if (dto.DepartmentId.HasValue) entity.DepartmentId = dto.DepartmentId;
            if (dto.Location != null) entity.Location = dto.Location;
            if (dto.ContractType != null) entity.ContractType = dto.ContractType;
            if (dto.Seniority != null) entity.Seniority = dto.Seniority;
            if (dto.Description != null) entity.Description = dto.Description;
            if (dto.Requirements != null) entity.Requirements = dto.Requirements;
            if (dto.SalaryMin.HasValue) entity.SalaryMin = dto.SalaryMin;
            if (dto.SalaryMax.HasValue) entity.SalaryMax = dto.SalaryMax;
            if (!string.IsNullOrWhiteSpace(dto.Currency)) entity.Currency = dto.Currency;
            if (dto.OpeningsCount.HasValue) entity.OpeningsCount = Math.Max(1, dto.OpeningsCount.Value);
            if (!string.IsNullOrWhiteSpace(dto.Status))
            {
                if (entity.Status != "open" && dto.Status == "open" && entity.OpenedAt == null)
                    entity.OpenedAt = DateTime.UtcNow;
                if (entity.Status != "closed" && entity.Status != "filled" && (dto.Status == "closed" || dto.Status == "filled"))
                    entity.ClosedAt = DateTime.UtcNow;
                entity.Status = dto.Status;
            }
            if (dto.HiringManagerUserId.HasValue) entity.HiringManagerUserId = dto.HiringManagerUserId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "job_opening_updated", $"Opening '{entity.Title}' updated", new { entity.Id, entity.Status }, actorUserId);
            return await MapOpeningAsync(entity);
        }

        public async Task DeleteJobOpeningAsync(int id, int actorUserId)
        {
            var entity = await _db.HrJobOpenings.FirstOrDefaultAsync(o => o.Id == id);
            if (entity == null) return;
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "job_opening_deleted", $"Opening '{entity.Title}' deleted", new { entity.Id }, actorUserId);
        }

        // =========================================================================
        // RECRUITMENT: APPLICANTS
        // =========================================================================
        private async Task<HrApplicantDto> MapApplicantAsync(HrApplicant a)
        {
            var openingTitle = await _db.HrJobOpenings.Where(o => o.Id == a.OpeningId).Select(o => o.Title).FirstOrDefaultAsync();
            var interviewsCount = await _db.HrInterviews.CountAsync(i => i.ApplicantId == a.Id && !i.IsDeleted);
            var notesCount = await _db.HrApplicantNotes.CountAsync(n => n.ApplicantId == a.Id);
            return new HrApplicantDto
            {
                Id = a.Id, OpeningId = a.OpeningId, OpeningTitle = openingTitle,
                FirstName = a.FirstName, LastName = a.LastName,
                Email = a.Email, Phone = a.Phone, Source = a.Source,
                ResumeUrl = a.ResumeUrl, ResumeFileName = a.ResumeFileName,
                Stage = a.Stage, Rating = a.Rating, ExpectedSalary = a.ExpectedSalary,
                AvailableFrom = a.AvailableFrom, RejectionReason = a.RejectionReason,
                CreatedAt = a.CreatedAt, InterviewsCount = interviewsCount, NotesCount = notesCount,
            };
        }

        public async Task<List<HrApplicantDto>> GetApplicantsAsync(int? openingId, string? stage)
        {
            var q = _db.HrApplicants.Where(a => !a.IsDeleted);
            if (openingId.HasValue) q = q.Where(a => a.OpeningId == openingId.Value);
            if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(a => a.Stage == stage);
            var rows = await q.OrderByDescending(a => a.CreatedAt).ToListAsync();
            if (rows.Count == 0) return new List<HrApplicantDto>();

            var openingIds = rows.Select(a => a.OpeningId).Distinct().ToList();
            var openingTitles = await _db.HrJobOpenings
                .Where(o => openingIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.Title);

            var applicantIds = rows.Select(a => a.Id).ToList();
            var interviewCounts = await _db.HrInterviews
                .Where(i => applicantIds.Contains(i.ApplicantId) && !i.IsDeleted)
                .GroupBy(i => i.ApplicantId)
                .Select(g => new { ApplicantId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.ApplicantId, g => g.Count);

            var noteCounts = await _db.HrApplicantNotes
                .Where(n => applicantIds.Contains(n.ApplicantId))
                .GroupBy(n => n.ApplicantId)
                .Select(g => new { ApplicantId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.ApplicantId, g => g.Count);

            return rows.Select(a => new HrApplicantDto
            {
                Id = a.Id, OpeningId = a.OpeningId,
                OpeningTitle = openingTitles.TryGetValue(a.OpeningId, out var ot) ? ot : null,
                FirstName = a.FirstName, LastName = a.LastName,
                Email = a.Email, Phone = a.Phone, Source = a.Source,
                ResumeUrl = a.ResumeUrl, ResumeFileName = a.ResumeFileName,
                Stage = a.Stage, Rating = a.Rating, ExpectedSalary = a.ExpectedSalary,
                AvailableFrom = a.AvailableFrom, RejectionReason = a.RejectionReason,
                CreatedAt = a.CreatedAt,
                InterviewsCount = interviewCounts.TryGetValue(a.Id, out var ic) ? ic : 0,
                NotesCount = noteCounts.TryGetValue(a.Id, out var nc) ? nc : 0,
            }).ToList();
        }

        public async Task<HrApplicantDto> GetApplicantAsync(int id)
        {
            var a = await _db.HrApplicants.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                ?? throw new KeyNotFoundException("hr.applicant_not_found");
            return await MapApplicantAsync(a);
        }

        public async Task<HrApplicantDto> CreateApplicantAsync(UpsertHrApplicantDto dto, int actorUserId)
        {
            var entity = new HrApplicant
            {
                OpeningId = dto.OpeningId,
                FirstName = dto.FirstName?.Trim() ?? string.Empty,
                LastName = dto.LastName?.Trim() ?? string.Empty,
                Email = dto.Email, Phone = dto.Phone, Source = dto.Source,
                ResumeUrl = dto.ResumeUrl, ResumeFileName = dto.ResumeFileName,
                Stage = dto.Stage ?? "applied",
                Rating = dto.Rating, ExpectedSalary = dto.ExpectedSalary,
                AvailableFrom = dto.AvailableFrom, RejectionReason = dto.RejectionReason,
                CreatedBy = actorUserId == 0 ? null : actorUserId,
            };
            _db.HrApplicants.Add(entity);
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "applicant_created", $"Applicant '{entity.FirstName} {entity.LastName}' added", new { entity.Id, entity.OpeningId }, actorUserId);
            return await MapApplicantAsync(entity);
        }

        public async Task<HrApplicantDto> UpdateApplicantAsync(int id, UpsertHrApplicantDto dto, int actorUserId)
        {
            var entity = await _db.HrApplicants.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted)
                ?? throw new KeyNotFoundException("hr.applicant_not_found");
            if (dto.OpeningId > 0) entity.OpeningId = dto.OpeningId;
            if (!string.IsNullOrWhiteSpace(dto.FirstName)) entity.FirstName = dto.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.LastName)) entity.LastName = dto.LastName.Trim();
            if (dto.Email != null) entity.Email = dto.Email;
            if (dto.Phone != null) entity.Phone = dto.Phone;
            if (dto.Source != null) entity.Source = dto.Source;
            if (dto.ResumeUrl != null) entity.ResumeUrl = dto.ResumeUrl;
            if (dto.ResumeFileName != null) entity.ResumeFileName = dto.ResumeFileName;
            if (!string.IsNullOrWhiteSpace(dto.Stage)) entity.Stage = dto.Stage;
            if (dto.Rating.HasValue) entity.Rating = dto.Rating;
            if (dto.ExpectedSalary.HasValue) entity.ExpectedSalary = dto.ExpectedSalary;
            if (dto.AvailableFrom.HasValue) entity.AvailableFrom = dto.AvailableFrom;
            if (dto.RejectionReason != null) entity.RejectionReason = dto.RejectionReason;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "applicant_updated", $"Applicant updated ({entity.Stage})", new { entity.Id, entity.Stage }, actorUserId);
            return await MapApplicantAsync(entity);
        }

        public async Task<HrApplicantDto> MoveApplicantStageAsync(int id, MoveApplicantStageDto dto, int actorUserId)
        {
            var entity = await _db.HrApplicants.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted)
                ?? throw new KeyNotFoundException("hr.applicant_not_found");
            entity.Stage = dto.Stage ?? "applied";
            if (entity.Stage == "rejected") entity.RejectionReason = dto.RejectionReason;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "applicant_stage_moved", $"Moved to {entity.Stage}",
                new { entity.Id, entity.Stage, entity.RejectionReason }, actorUserId);
            return await MapApplicantAsync(entity);
        }

        public async Task DeleteApplicantAsync(int id, int actorUserId)
        {
            var entity = await _db.HrApplicants.FirstOrDefaultAsync(a => a.Id == id);
            if (entity == null) return;
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "applicant_deleted", "Applicant deleted", new { entity.Id }, actorUserId);
        }

        // =========================================================================
        // RECRUITMENT: INTERVIEWS
        // =========================================================================
        private async Task<HrInterviewDto> MapInterviewAsync(HrInterview i)
        {
            var applicant = await _db.HrApplicants
                .Where(a => a.Id == i.ApplicantId)
                .Select(a => new { a.FirstName, a.LastName }).FirstOrDefaultAsync();
            var interviewerName = i.InterviewerUserId.HasValue
                ? (await GetUserNamesAsync(new[] { i.InterviewerUserId.Value })).GetValueOrDefault(i.InterviewerUserId.Value)
                : null;
            return new HrInterviewDto
            {
                Id = i.Id, ApplicantId = i.ApplicantId,
                ApplicantName = applicant != null ? $"{applicant.FirstName} {applicant.LastName}".Trim() : null,
                Kind = i.Kind, ScheduledAt = i.ScheduledAt, DurationMinutes = i.DurationMinutes,
                InterviewerUserId = i.InterviewerUserId, InterviewerName = interviewerName,
                Location = i.Location, MeetingUrl = i.MeetingUrl,
                Status = i.Status, Score = i.Score, Feedback = i.Feedback, Recommendation = i.Recommendation,
            };
        }

        public async Task<List<HrInterviewDto>> GetInterviewsAsync(int? applicantId, DateTime? from, DateTime? to)
        {
            var q = _db.HrInterviews.Where(i => !i.IsDeleted);
            if (applicantId.HasValue) q = q.Where(i => i.ApplicantId == applicantId.Value);
            if (from.HasValue) q = q.Where(i => i.ScheduledAt >= from.Value);
            if (to.HasValue) q = q.Where(i => i.ScheduledAt <= to.Value);
            var rows = await q.OrderBy(i => i.ScheduledAt).ToListAsync();
            if (rows.Count == 0) return new List<HrInterviewDto>();

            var applicantIds = rows.Select(i => i.ApplicantId).Distinct().ToList();
            var applicantNames = await _db.HrApplicants
                .Where(a => applicantIds.Contains(a.Id))
                .Select(a => new { a.Id, a.FirstName, a.LastName })
                .ToDictionaryAsync(a => a.Id, a => $"{a.FirstName} {a.LastName}".Trim());

            var interviewerIds = rows.Where(i => i.InterviewerUserId.HasValue).Select(i => i.InterviewerUserId!.Value).Distinct();
            var interviewerNames = await GetUserNamesAsync(interviewerIds);

            return rows.Select(i => new HrInterviewDto
            {
                Id = i.Id, ApplicantId = i.ApplicantId,
                ApplicantName = applicantNames.TryGetValue(i.ApplicantId, out var an) ? an : null,
                Kind = i.Kind, ScheduledAt = i.ScheduledAt, DurationMinutes = i.DurationMinutes,
                InterviewerUserId = i.InterviewerUserId,
                InterviewerName = i.InterviewerUserId.HasValue && interviewerNames.TryGetValue(i.InterviewerUserId.Value, out var inn) ? inn : null,
                Location = i.Location, MeetingUrl = i.MeetingUrl,
                Status = i.Status, Score = i.Score, Feedback = i.Feedback, Recommendation = i.Recommendation,
            }).ToList();
        }

        public async Task<HrInterviewDto> CreateInterviewAsync(UpsertHrInterviewDto dto, int actorUserId)
        {
            var entity = new HrInterview
            {
                ApplicantId = dto.ApplicantId,
                Kind = dto.Kind ?? "phone",
                ScheduledAt = dto.ScheduledAt,
                DurationMinutes = Math.Max(15, dto.DurationMinutes ?? 45),
                InterviewerUserId = dto.InterviewerUserId,
                Location = dto.Location, MeetingUrl = dto.MeetingUrl,
                Status = dto.Status ?? "scheduled",
                Score = dto.Score, Feedback = dto.Feedback, Recommendation = dto.Recommendation,
                CreatedBy = actorUserId == 0 ? null : actorUserId,
            };
            _db.HrInterviews.Add(entity);
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "interview_scheduled", $"Interview {entity.Kind}", new { entity.Id, entity.ApplicantId }, actorUserId);
            return await MapInterviewAsync(entity);
        }

        public async Task<HrInterviewDto> UpdateInterviewAsync(int id, UpsertHrInterviewDto dto, int actorUserId)
        {
            var entity = await _db.HrInterviews.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted)
                ?? throw new KeyNotFoundException("hr.interview_not_found");
            if (dto.ApplicantId > 0) entity.ApplicantId = dto.ApplicantId;
            if (!string.IsNullOrWhiteSpace(dto.Kind)) entity.Kind = dto.Kind;
            if (dto.ScheduledAt != default) entity.ScheduledAt = dto.ScheduledAt;
            if (dto.DurationMinutes.HasValue) entity.DurationMinutes = Math.Max(15, dto.DurationMinutes.Value);
            if (dto.InterviewerUserId.HasValue) entity.InterviewerUserId = dto.InterviewerUserId;
            if (dto.Location != null) entity.Location = dto.Location;
            if (dto.MeetingUrl != null) entity.MeetingUrl = dto.MeetingUrl;
            if (!string.IsNullOrWhiteSpace(dto.Status)) entity.Status = dto.Status;
            if (dto.Score.HasValue) entity.Score = dto.Score;
            if (dto.Feedback != null) entity.Feedback = dto.Feedback;
            if (dto.Recommendation != null) entity.Recommendation = dto.Recommendation;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "interview_updated", $"Interview {entity.Status}", new { entity.Id, entity.Status }, actorUserId);
            return await MapInterviewAsync(entity);
        }

        public async Task DeleteInterviewAsync(int id, int actorUserId)
        {
            var entity = await _db.HrInterviews.FirstOrDefaultAsync(i => i.Id == id);
            if (entity == null) return;
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogPerfRecruitAsync(null, "interview_deleted", "Interview deleted", new { entity.Id }, actorUserId);
        }

        // =========================================================================
        // RECRUITMENT: NOTES
        // =========================================================================
        public async Task<List<HrApplicantNoteDto>> GetApplicantNotesAsync(int applicantId)
        {
            var rows = await _db.HrApplicantNotes
                .Where(n => n.ApplicantId == applicantId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            var names = await GetUserNamesAsync(rows.Where(r => r.AuthorUserId.HasValue).Select(r => r.AuthorUserId!.Value));
            return rows.Select(n => new HrApplicantNoteDto
            {
                Id = n.Id, ApplicantId = n.ApplicantId,
                AuthorUserId = n.AuthorUserId,
                AuthorName = n.AuthorUserId.HasValue ? names.GetValueOrDefault(n.AuthorUserId.Value) : null,
                Body = n.Body, CreatedAt = n.CreatedAt,
            }).ToList();
        }

        public async Task<HrApplicantNoteDto> AddApplicantNoteAsync(UpsertHrApplicantNoteDto dto, int actorUserId)
        {
            var entity = new HrApplicantNote
            {
                ApplicantId = dto.ApplicantId,
                Body = dto.Body ?? string.Empty,
                AuthorUserId = actorUserId == 0 ? null : actorUserId,
            };
            _db.HrApplicantNotes.Add(entity);
            await _db.SaveChangesAsync();
            return (await GetApplicantNotesAsync(dto.ApplicantId)).First(n => n.Id == entity.Id);
        }

        public async Task DeleteApplicantNoteAsync(int id, int actorUserId)
        {
            var entity = await _db.HrApplicantNotes.FirstOrDefaultAsync(n => n.Id == id);
            if (entity == null) return;
            _db.HrApplicantNotes.Remove(entity);
            await _db.SaveChangesAsync();
        }

        // =========================================================================
        // RECRUITMENT: DASHBOARD
        // =========================================================================
        public async Task<RecruitmentDashboardDto> GetRecruitmentDashboardAsync()
        {
            var openPositions = await _db.HrJobOpenings.CountAsync(o => !o.IsDeleted && o.Status == "open");
            var activeStages = new[] { "applied", "screening", "interview", "offer" };
            var activeApplicants = await _db.HrApplicants.CountAsync(a => !a.IsDeleted && activeStages.Contains(a.Stage));
            var weekStart = DateTime.UtcNow.Date;
            var weekEnd = weekStart.AddDays(7);
            var interviewsThisWeek = await _db.HrInterviews
                .CountAsync(i => !i.IsDeleted && i.ScheduledAt >= weekStart && i.ScheduledAt < weekEnd && i.Status == "scheduled");
            var offersOut = await _db.HrApplicants.CountAsync(a => !a.IsDeleted && a.Stage == "offer");
            var byStage = await _db.HrApplicants
                .Where(a => !a.IsDeleted)
                .GroupBy(a => a.Stage)
                .Select(g => new { Stage = g.Key, Count = g.Count() })
                .ToListAsync();
            return new RecruitmentDashboardDto
            {
                OpenPositions = openPositions,
                ActiveApplicants = activeApplicants,
                InterviewsThisWeek = interviewsThisWeek,
                OffersOut = offersOut,
                ByStage = byStage.ToDictionary(x => x.Stage, x => x.Count),
            };
        }
    }
}