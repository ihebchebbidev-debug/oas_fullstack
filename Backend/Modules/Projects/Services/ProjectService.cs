using MyApi.Data;
using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Models;
using MyApi.Modules.Contacts.Models;
using MyApi.Modules.Offers.Models;
using MyApi.Modules.Sales.Models;
using MyApi.Modules.ServiceOrders.Models;
using MyApi.Modules.Dispatches.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MyApi.Modules.Projects.Services
{
    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProjectService> _logger;
        private readonly IProjectColumnService _columnService;

        public ProjectService(ApplicationDbContext context, ILogger<ProjectService> logger, IProjectColumnService columnService)
        {
            _context = context;
            _logger = logger;
            _columnService = columnService;
        }

        public async Task<ProjectListResponseDto> GetAllProjectsAsync(ProjectSearchRequestDto? searchRequest = null)
        {
            try
            {
                // ✅ OPTIMIZATION: Remove eager loading for list view (3-5x faster)
                var query = _context.Projects
                    .AsNoTracking()
                    // Removed: .Include(p => p.Columns) - only needed for detail view
                    // Removed: .Include(p => p.Contact) - only needed for detail view
                    .AsQueryable();

                // Apply filters
                if (searchRequest != null)
                {
                    if (!string.IsNullOrWhiteSpace(searchRequest.SearchTerm))
                    {
                        // Index-friendly, case-insensitive match (same pattern as SearchProjectsAsync).
                        var pattern = $"%{searchRequest.SearchTerm.Trim()}%";
                        query = query.Where(p => EF.Functions.ILike(p.Name, pattern) ||
                                               (p.Description != null && EF.Functions.ILike(p.Description, pattern)));
                    }


                    if (!string.IsNullOrEmpty(searchRequest.Status))
                        query = query.Where(p => p.Status == searchRequest.Status);

                    if (!string.IsNullOrEmpty(searchRequest.Priority))
                        query = query.Where(p => p.Priority == searchRequest.Priority);

                    if (searchRequest.ContactId.HasValue)
                        query = query.Where(p => p.ContactId == searchRequest.ContactId.Value);

                    // Date range filters
                    if (searchRequest.StartDateFrom.HasValue)
                        query = query.Where(p => p.StartDate >= searchRequest.StartDateFrom.Value);

                    if (searchRequest.StartDateTo.HasValue)
                        query = query.Where(p => p.StartDate <= searchRequest.StartDateTo.Value);

                    if (searchRequest.EndDateFrom.HasValue)
                        query = query.Where(p => p.EndDate >= searchRequest.EndDateFrom.Value);

                    if (searchRequest.EndDateTo.HasValue)
                        query = query.Where(p => p.EndDate <= searchRequest.EndDateTo.Value);

                    // Archived projects are modelled as Status == "archived".
                    if (searchRequest.IsArchived.HasValue)
                        query = searchRequest.IsArchived.Value
                            ? query.Where(p => p.Status == "archived")
                            : query.Where(p => p.Status != "archived");

                    // Apply sorting
                    if (!string.IsNullOrEmpty(searchRequest.SortBy))
                    {
                        var isDescending = searchRequest.SortDirection?.ToLower() == "desc";
                        
                        query = searchRequest.SortBy.ToLower() switch
                        {
                            "name" => isDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                            "status" => isDescending ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
                            "priority" => isDescending ? query.OrderByDescending(p => p.Priority) : query.OrderBy(p => p.Priority),
                            "startdate" => isDescending ? query.OrderByDescending(p => p.StartDate) : query.OrderBy(p => p.StartDate),
                            "enddate" => isDescending ? query.OrderByDescending(p => p.EndDate) : query.OrderBy(p => p.EndDate),
                            _ => query.OrderByDescending(p => p.CreatedDate)
                        };
                    }
                    else
                    {
                        query = query.OrderByDescending(p => p.CreatedDate);
                    }
                }
                else
                {
                    query = query.OrderByDescending(p => p.CreatedDate);
                }

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply pagination
                // Clamp pagination to safe bounds (mirrors Deals: page >= 1, size 1..200).
                var pageNumber = Math.Max(1, searchRequest?.PageNumber ?? 1);
                var pageSize = Math.Clamp(searchRequest?.PageSize ?? 20, 1, 200);
                var skip = (pageNumber - 1) * pageSize;

                var projects = await query
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                var projectDtos = projects.Select(MapToProjectDto).ToList();

                return new ProjectListResponseDto
                {
                    Projects = projectDtos,
                    TotalCount = totalCount,
                    PageSize = pageSize,
                    PageNumber = pageNumber,
                    HasNextPage = skip + pageSize < totalCount,
                    HasPreviousPage = pageNumber > 1
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all projects");
                throw;
            }
        }

        public async Task<ProjectResponseDto?> GetProjectByIdAsync(int id)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.Contact)
                    .Where(p => p.Id == id)
                    .FirstOrDefaultAsync();

                if (project == null) return null;
                var dto = MapToProjectDto(project);
                dto.Settings = await GetProjectSettingsAsync();

                var columnsResult = await _columnService.GetProjectColumnsAsync(id);
                dto.Columns = columnsResult.Columns
                    .Select(c => new ProjectColumnDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        DisplayOrder = c.DisplayOrder,
                        Color = c.Color
                    })
                    .ToList();

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project by id {ProjectId}", id);
                throw;
            }
        }

        // Npgsql is configured with a retrying execution strategy, which forbids
        // user-initiated transactions unless the whole unit runs inside the strategy.
        // Every transactional method below therefore goes through CreateExecutionStrategy().
        public Task<ProjectResponseDto> CreateProjectAsync(CreateProjectRequestDto createDto, string createdByUser) =>
            _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => CreateProjectCoreAsync(createDto, createdByUser));

        private async Task<ProjectResponseDto> CreateProjectCoreAsync(CreateProjectRequestDto createDto, string createdByUser)
        {
            // Validate enum-like fields up front so a typo can never reach the DB
            // (statistics/kanban grouping silently break on unknown values).
            var status = NormalizeStatus(createDto.Status);
            var kind = NormalizeKind(createDto.ProjectKind);
            var priority = NormalizePriority(createDto.Priority);

            // Nesting-safe: when an outer unit of work already owns a transaction
            // (e.g. Deal → Project conversion), join it instead of opening a second
            // one — Npgsql rejects nested transactions on the same connection.
            var ownsTx = _context.Database.CurrentTransaction == null;
            await using var tx = ownsTx ? await _context.Database.BeginTransactionAsync() : null;
            try
            {
                if (createDto.ContactId.HasValue)
                {
                    var contactExists = await _context.Contacts.AnyAsync(c => c.Id == createDto.ContactId.Value);
                    if (!contactExists)
                        throw new InvalidOperationException($"Contact {createDto.ContactId.Value} not found");
                }

                var project = new Project
                {
                    Name = createDto.Name,
                    Description = createDto.Description,
                    ContactId = createDto.ContactId,
                    TeamMembers = SerializeTeamMembers(createDto.TeamMembers),
                    Status = status,
                    ProjectKind = kind,
                    Priority = priority,
                    Budget = createDto.Budget,
                    StartDate = createDto.StartDate,
                    EndDate = createDto.EndDate,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = createdByUser
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                ProjectAutoNote.Add(_context, project.Id,
                    $"Project '{project.Name}' created (kind: {project.ProjectKind}, status: {project.Status}).",
                    createdByUser);

                await SetProjectIdForLinkedEntitiesAsync(project.Id, createDto.LinkOfferId, createDto.LinkSaleId, createDto.LinkServiceOrderId, createDto.LinkDispatchId);

                // Create default columns if needed
                if (createDto.CreateDefaultColumns)
                {
                    await _columnService.CreateDefaultColumnsAsync(project.Id, createdByUser);
                }

                if (tx != null) await tx.CommitAsync();

                // Reload with includes (after commit so the read sees the final state)
                var createdProject = await GetProjectByIdAsync(project.Id);
                _logger.LogInformation("Project created successfully with ID {ProjectId}", project.Id);

                return createdProject!;
            }
            catch (Exception ex)
            {
                if (tx != null) await tx.RollbackAsync();
                _logger.LogError(ex, "Error creating project");
                throw;
            }
        }


        public Task<ProjectResponseDto?> UpdateProjectAsync(int id, UpdateProjectRequestDto updateDto, string modifiedByUser) =>
            _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => UpdateProjectCoreAsync(id, updateDto, modifiedByUser));

        private async Task<ProjectResponseDto?> UpdateProjectCoreAsync(int id, UpdateProjectRequestDto updateDto, string modifiedByUser)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var project = await _context.Projects
                    .Where(p => p.Id == id)
                    .FirstOrDefaultAsync();

                if (project == null)
                    return null;

                // Update fields
                if (!string.IsNullOrEmpty(updateDto.Name))
                    project.Name = updateDto.Name;

                if (updateDto.Description != null)
                    project.Description = updateDto.Description;

                if (updateDto.ContactId.HasValue)
                {
                    var contactExists = await _context.Contacts.AnyAsync(c => c.Id == updateDto.ContactId.Value);
                    if (!contactExists)
                        throw new InvalidOperationException($"Contact {updateDto.ContactId.Value} not found");
                    project.ContactId = updateDto.ContactId;
                }

                if (updateDto.TeamMembers != null)
                    project.TeamMembers = SerializeTeamMembers(updateDto.TeamMembers);

                var oldStatus = project.Status;
                var oldKind = project.ProjectKind;

                if (!string.IsNullOrEmpty(updateDto.Status))
                    project.Status = NormalizeStatus(updateDto.Status);

                if (!string.IsNullOrEmpty(updateDto.ProjectKind))
                    project.ProjectKind = NormalizeKind(updateDto.ProjectKind);

                if (!string.IsNullOrEmpty(updateDto.Priority))
                    project.Priority = NormalizePriority(updateDto.Priority);

                if (updateDto.StartDate.HasValue)
                    project.StartDate = updateDto.StartDate;

                if (updateDto.EndDate.HasValue)
                    project.EndDate = updateDto.EndDate;

                project.ModifiedBy = modifiedByUser;
                project.ModifiedDate = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(updateDto.Status) && oldStatus != project.Status)
                    ProjectAutoNote.Add(_context, id, $"Status changed: {oldStatus} → {project.Status}.", modifiedByUser);
                if (!string.IsNullOrEmpty(updateDto.ProjectKind) && oldKind != project.ProjectKind)
                    ProjectAutoNote.Add(_context, id, $"Kind changed: {oldKind} → {project.ProjectKind}.", modifiedByUser);

                // Track plain detail edits (name/description/contact/team/priority/dates)
                // so every modification shows up on the project's timeline.
                var nonStatusEdit = !string.IsNullOrEmpty(updateDto.Name)
                    || updateDto.Description != null
                    || updateDto.ContactId.HasValue
                    || updateDto.TeamMembers != null
                    || !string.IsNullOrEmpty(updateDto.Priority)
                    || updateDto.StartDate.HasValue
                    || updateDto.EndDate.HasValue;
                if (nonStatusEdit)
                    ProjectAutoNote.Add(_context, id, "Project details updated.", modifiedByUser);

                await _context.SaveChangesAsync();
                await SetProjectIdForLinkedEntitiesAsync(id, updateDto.LinkOfferId, updateDto.LinkSaleId, updateDto.LinkServiceOrderId, updateDto.LinkDispatchId);

                await tx.CommitAsync();

                var updatedProject = await GetProjectByIdAsync(id);
                _logger.LogInformation("Project updated successfully with ID {ProjectId}", id);

                return updatedProject;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error updating project with ID {ProjectId}", id);
                throw;
            }
        }

        public Task<bool> DeleteProjectAsync(int id, string deletedByUser) =>
            _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => DeleteProjectCoreAsync(id, deletedByUser));

        private async Task<bool> DeleteProjectCoreAsync(int id, string deletedByUser)
        {
            // ProjectTask/ProjectColumn/ProjectNote/ProjectActivity have no FK to Projects
            // (ProjectTask is entity-agnostic), so there is no DB-level cascade. Clean up
            // every child row explicitly inside one transaction, otherwise deleting a
            // project leaves orphaned tasks/columns/notes/activities behind forever.
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var project = await _context.Projects
                    .Where(p => p.Id == id)
                    .FirstOrDefaultAsync();

                if (project == null)
                    return false;

                var columns = await _context.Set<ProjectColumn>().Where(c => c.ProjectId == id).ToListAsync();
                if (columns.Count > 0) _context.Set<ProjectColumn>().RemoveRange(columns);

                var notes = await _context.Set<ProjectNote>().Where(n => n.ProjectId == id).ToListAsync();
                if (notes.Count > 0) _context.Set<ProjectNote>().RemoveRange(notes);

                var activities = await _context.Set<ProjectActivity>().Where(a => a.ProjectId == id).ToListAsync();
                if (activities.Count > 0) _context.Set<ProjectActivity>().RemoveRange(activities);

                var tasks = await _context.Set<ProjectTask>()
                    .Where(t => t.RelatedEntityType != null
                        && t.RelatedEntityType.ToLower() == "project"
                        && t.RelatedEntityId == id)
                    .ToListAsync();
                if (tasks.Count > 0) _context.Set<ProjectTask>().RemoveRange(tasks);

                // Detach linked business documents instead of deleting them.
                foreach (var o in await _context.Offers.Where(o => o.ProjectId == id).ToListAsync())
                    o.ProjectId = null;
                foreach (var s in await _context.Sales.Where(s => s.ProjectId == id).ToListAsync())
                    s.ProjectId = null;
                foreach (var s in await _context.ServiceOrders.Where(s => s.ProjectId == id).ToListAsync())
                    s.ProjectId = null;
                foreach (var d in await _context.Dispatches.Where(d => d.ProjectId == id).ToListAsync())
                    d.ProjectId = null;

                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation(
                    "Project {ProjectId} deleted by {User} (cleaned {Columns} columns, {Notes} notes, {Activities} activities, {Tasks} tasks)",
                    id, deletedByUser, columns.Count, notes.Count, activities.Count, tasks.Count);
                return true;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error deleting project with ID {ProjectId}", id);
                throw;
            }
        }


        public async Task<ProjectStatisticsDto> GetStatisticsAsync()
        {
            try
            {
                // Server-side aggregation: one SELECT per grouping, no full-table load.
                var statusCounts = await _context.Projects
                    .GroupBy(p => p.Status ?? "")
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status, x => x.Count);

                var priorityCounts = await _context.Projects
                    .GroupBy(p => p.Priority ?? "")
                    .Select(g => new { Priority = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Priority, x => x.Count);

                var total = statusCounts.Values.Sum();

                return new ProjectStatisticsDto
                {
                    TotalProjects = total,
                    ActiveProjects = statusCounts.TryGetValue("active", out var a) ? a : 0,
                    CompletedProjects = statusCounts.TryGetValue("completed", out var c) ? c : 0,
                    OnHoldProjects = statusCounts.TryGetValue("on-hold", out var h) ? h : 0,
                    HighPriorityCount = priorityCounts.TryGetValue("high", out var hp) ? hp : 0,
                    MediumPriorityCount = priorityCounts.TryGetValue("medium", out var mp) ? mp : 0,
                    LowPriorityCount = priorityCounts.TryGetValue("low", out var lp) ? lp : 0,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project statistics");
                throw;
            }
        }

        public Task<int> BulkUpdateStatusAsync(List<int> projectIds, string status, string userId) =>
            _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => BulkUpdateStatusCoreAsync(projectIds, status, userId));

        private async Task<int> BulkUpdateStatusCoreAsync(List<int> projectIds, string status, string userId)
        {
            if (projectIds == null || projectIds.Count == 0) return 0;
            var normalized = NormalizeStatus(status);

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var projects = await _context.Projects.Where(p => projectIds.Contains(p.Id)).ToListAsync();
                foreach (var project in projects)
                {
                    if (project.Status == normalized) continue;
                    var oldStatus = project.Status;
                    project.Status = normalized;
                    project.ModifiedBy = userId;
                    project.ModifiedDate = DateTime.UtcNow;
                    ProjectAutoNote.Add(_context, project.Id, $"Status changed from {oldStatus} to {normalized}", userId);
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return projects.Count;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error bulk updating project status");
                throw;
            }
        }

        // Archiving is modelled as Status == "archived"; un-archiving restores "active".
        public Task<int> BulkArchiveAsync(List<int> projectIds, bool archive, string userId) =>
            BulkUpdateStatusAsync(projectIds, archive ? "archived" : "active", userId);

        public async Task<List<ProjectResponseDto>> SearchProjectsAsync(string searchTerm)
        {
            try
            {
                // An empty term would become LIKE '%%' — a full table scan returning an
                // arbitrary 50 rows. Return nothing instead.
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return new List<ProjectResponseDto>();

                // ILike = index-friendly, case-insensitive matching (no .ToLower() scan).
                var pattern = $"%{searchTerm.Trim()}%";
                var projects = await _context.Projects
                    .AsNoTracking()
                    .Include(p => p.Contact)
                    .Where(p => EF.Functions.ILike(p.Name, pattern)
                        || (p.Description != null && EF.Functions.ILike(p.Description, pattern)))
                    .OrderByDescending(p => p.CreatedDate)
                    .Take(50)
                    .ToListAsync();

                return projects.Select(MapToProjectDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching projects");
                throw;
            }
        }

        // ---- Enum-like field validation -------------------------------------------------
        private static readonly HashSet<string> AllowedStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "active", "completed", "on-hold", "cancelled", "planning", "archived" };
        private static readonly HashSet<string> AllowedKinds =
            new(StringComparer.OrdinalIgnoreCase) { "client", "internal" };
        private static readonly HashSet<string> AllowedPriorities =
            new(StringComparer.OrdinalIgnoreCase) { "low", "medium", "high", "urgent" };

        private static string NormalizeStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "active";
            var v = value.Trim().ToLowerInvariant();
            if (!AllowedStatuses.Contains(v))
                throw new InvalidOperationException($"Invalid status '{value}'. Allowed: {string.Join(", ", AllowedStatuses)}");
            return v;
        }

        private static string NormalizeKind(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "client";
            var v = value.Trim().ToLowerInvariant();
            if (!AllowedKinds.Contains(v))
                throw new InvalidOperationException($"Invalid projectKind '{value}'. Allowed: {string.Join(", ", AllowedKinds)}");
            return v;
        }

        private static string NormalizePriority(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "medium";
            var v = value.Trim().ToLowerInvariant();
            if (!AllowedPriorities.Contains(v))
                throw new InvalidOperationException($"Invalid priority '{value}'. Allowed: {string.Join(", ", AllowedPriorities)}");
            return v;
        }

        private static string SerializeTeamMembers(List<int>? teamMembers)
        {
            // Stored as JSON in Projects.TeamMembers (varchar). De-duplicated so the
            // blob can never accumulate the same user twice.
            return JsonSerializer.Serialize((teamMembers ?? new List<int>()).Distinct().ToList());
        }

        private List<int> DeserializeTeamMembers(string? teamMembersJson)
        {
            if (string.IsNullOrWhiteSpace(teamMembersJson))
                return new List<int>();

            try
            {
                return JsonSerializer.Deserialize<List<int>>(teamMembersJson) ?? new List<int>();
            }
            catch (Exception ex)
            {
                // Fail safely, but never silently — this indicates corrupted data.
                _logger.LogWarning(ex, "Corrupted TeamMembers JSON encountered: {Json}", teamMembersJson);
                return new List<int>();
            }
        }


        public async Task<List<ProjectNoteDto>> GetProjectNotesAsync(int projectId)
        {
            try
            {
                var notes = await _context.Set<ProjectNote>()
                    .Where(n => n.ProjectId == projectId)
                    .OrderByDescending(n => n.CreatedDate)
                    .AsNoTracking()
                    .ToListAsync();

                return notes.Select(n => new ProjectNoteDto
                {
                    Id = n.Id,
                    ProjectId = n.ProjectId,
                    Content = n.Content,
                    CreatedDate = n.CreatedDate,
                    CreatedBy = n.CreatedBy,
                    ModifiedDate = n.ModifiedDate,
                    ModifiedBy = n.ModifiedBy
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project notes for project {ProjectId}", projectId);
                throw;
            }
        }

        public Task<ProjectNoteDto> CreateProjectNoteAsync(int projectId, CreateProjectNoteRequestDto createDto, string createdByUser) =>
            _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => CreateProjectNoteCoreAsync(projectId, createDto, createdByUser));

        private async Task<ProjectNoteDto> CreateProjectNoteCoreAsync(int projectId, CreateProjectNoteRequestDto createDto, string createdByUser)
        {
            if (string.IsNullOrWhiteSpace(createDto.Content))
                throw new InvalidOperationException("Note content is required");

            // Note + activity must land together, otherwise the timeline desyncs.
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Verify project exists
                var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId);
                if (!projectExists)
                    throw new KeyNotFoundException($"Project with ID {projectId} not found");

                var note = new ProjectNote
                {
                    ProjectId = projectId,
                    Content = createDto.Content.Trim(),
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = createdByUser
                };

                _context.Set<ProjectNote>().Add(note);
                await _context.SaveChangesAsync();

                await _context.Set<ProjectActivity>().AddAsync(new ProjectActivity
                {
                    ProjectId = projectId,
                    ActionType = "note_added",
                    Description = "Note added",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = createdByUser,
                    RelatedEntityId = note.Id,
                    RelatedEntityType = "Note"
                });
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return new ProjectNoteDto
                {
                    Id = note.Id,
                    ProjectId = note.ProjectId,
                    Content = note.Content,
                    CreatedDate = note.CreatedDate,
                    CreatedBy = note.CreatedBy,
                    ModifiedDate = note.ModifiedDate,
                    ModifiedBy = note.ModifiedBy
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error creating project note for project {ProjectId}", projectId);
                throw;
            }
        }


        public async Task<bool> DeleteProjectNoteAsync(int noteId, string deletedByUser)
        {
            try
            {
                var note = await _context.Set<ProjectNote>().FindAsync(noteId);
                if (note == null)
                    return false;

                // Authorization: only the note creator may delete (server-side enforcement).
                if (!string.Equals(note.CreatedBy, deletedByUser, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Only the note creator can delete this note.");

                _context.Set<ProjectNote>().Remove(note);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project note {NoteId}", noteId);
                throw;
            }
        }

        public async Task<List<ProjectActivityDto>> GetProjectActivityAsync(int projectId)
        {
            try
            {
                var activities = await _context.Set<ProjectActivity>()
                    .Where(a => a.ProjectId == projectId)
                    .OrderByDescending(a => a.CreatedDate)
                    .AsNoTracking()
                    .ToListAsync();

                return activities.Select(a => new ProjectActivityDto
                {
                    Id = a.Id,
                    ProjectId = a.ProjectId,
                    ActionType = a.ActionType,
                    Description = a.Description,
                    Details = a.Details,
                    CreatedDate = a.CreatedDate,
                    CreatedBy = a.CreatedBy,
                    RelatedEntityId = a.RelatedEntityId,
                    RelatedEntityType = a.RelatedEntityType
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project activity for project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<ProjectLinksDto> GetProjectLinksAsync(int projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) throw new KeyNotFoundException($"Project with ID {projectId} not found");

            var offers = await _context.Offers
                .Where(o => !o.IsDeleted && o.ProjectId == projectId)
                .OrderByDescending(o => o.CreatedDate)
                .Take(100)
                .ToListAsync();
            var sales = await _context.Sales
                .Where(s => !s.IsDeleted && s.ProjectId == projectId)
                .OrderByDescending(s => s.CreatedDate)
                .Take(100)
                .ToListAsync();
            var serviceOrders = await _context.ServiceOrders
                .Where(s => !s.IsDeleted && s.ProjectId == projectId)
                .OrderByDescending(s => s.CreatedDate)
                .Take(100)
                .ToListAsync();
            var dispatches = await _context.Dispatches
                .Where(d => !d.IsDeleted && d.ProjectId == projectId)
                .OrderByDescending(d => d.CreatedDate)
                .Take(100)
                .ToListAsync();

            return new ProjectLinksDto
            {
                ProjectId = projectId,
                Offers = offers.Select(o => new ProjectLinkedEntityDto
                {
                    EntityType = "offer",
                    EntityId = o.Id,
                    Number = o.OfferNumber ?? $"OFR-{o.Id}",
                    Title = o.Title ?? "Offer",
                    Status = o.Status,
                    Date = o.CreatedDate,
                    Amount = o.TotalAmount
                }).ToList(),
                Sales = sales.Select(s => new ProjectLinkedEntityDto
                {
                    EntityType = "sale",
                    EntityId = s.Id,
                    Number = s.SaleNumber ?? $"SAL-{s.Id}",
                    Title = s.Title ?? "Sale",
                    Status = s.Status,
                    Date = s.CreatedDate,
                    IsDeal = s.IsDeal,
                    Amount = s.GrandTotal > 0 ? s.GrandTotal : s.TotalAmount
                }).ToList(),
                ServiceOrders = serviceOrders.Select(s => new ProjectLinkedEntityDto
                {
                    EntityType = "service_order",
                    EntityId = s.Id,
                    Number = s.OrderNumber ?? $"SO-{s.Id}",
                    Title = s.Description ?? "Service Order",
                    Status = s.Status,
                    Date = s.CreatedDate
                }).ToList(),
                Dispatches = dispatches.Select(d => new ProjectLinkedEntityDto
                {
                    EntityType = "dispatch",
                    EntityId = d.Id,
                    Number = d.DispatchNumber ?? $"DSP-{d.Id}",
                    Title = d.Description ?? "Dispatch",
                    Status = d.Status,
                    Date = d.CreatedDate
                }).ToList()
            };
        }

        public async Task<ProjectLinksDto> LinkEntityToProjectAsync(int projectId, LinkProjectEntityRequestDto dto, string userId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) throw new KeyNotFoundException($"Project with ID {projectId} not found");

            await SetEntityProjectAsync(dto.EntityType, dto.EntityId, projectId, userId);
            await _context.Set<ProjectActivity>().AddAsync(new ProjectActivity
            {
                ProjectId = projectId,
                ActionType = "linked_entity",
                Description = $"Linked {dto.EntityType} #{dto.EntityId} to project",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId,
                RelatedEntityId = dto.EntityId,
                RelatedEntityType = dto.EntityType
            });
            ProjectAutoNote.Add(_context, projectId, $"Linked {dto.EntityType} #{dto.EntityId} to the project.", userId);
            await _context.SaveChangesAsync();

            return await GetProjectLinksAsync(projectId);
        }

        public async Task<ProjectLinksDto> UnlinkEntityFromProjectAsync(int projectId, string entityType, int entityId, string userId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) throw new KeyNotFoundException($"Project with ID {projectId} not found");

            await SetEntityProjectAsync(entityType, entityId, null, userId);
            await _context.Set<ProjectActivity>().AddAsync(new ProjectActivity
            {
                ProjectId = projectId,
                ActionType = "unlinked_entity",
                Description = $"Unlinked {entityType} #{entityId} from project",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId,
                RelatedEntityId = entityId,
                RelatedEntityType = entityType
            });
            ProjectAutoNote.Add(_context, projectId, $"Unlinked {entityType} #{entityId} from the project.", userId);
            await _context.SaveChangesAsync();

            return await GetProjectLinksAsync(projectId);
        }

        public async Task<ProjectSettingsDto> GetProjectSettingsAsync()
        {
            // Deterministic pick: if duplicate rows ever exist, always read the same one.
            var settings = await _context.Set<ProjectSettings>()
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();
            if (settings == null)
            {
                return new ProjectSettingsDto();
            }
            try
            {
                return JsonSerializer.Deserialize<ProjectSettingsDto>(settings.SettingsJson) ?? new ProjectSettingsDto();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Corrupted ProjectSettings JSON on row {SettingsId}", settings.Id);
                return new ProjectSettingsDto();
            }
        }

        public async Task<ProjectSettingsDto> UpdateProjectSettingsAsync(ProjectSettingsDto dto, string userId)
        {
            var settings = await _context.Set<ProjectSettings>()
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new ProjectSettings();
                _context.Set<ProjectSettings>().Add(settings);
            }

            settings.SettingsJson = JsonSerializer.Serialize(dto);
            settings.UpdatedAt = DateTime.UtcNow;
            settings.UpdatedBy = userId;
            await _context.SaveChangesAsync();

            return dto;
        }

        public async Task<List<int>> GetTeamMembersAsync(int projectId)
        {
            var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) throw new KeyNotFoundException($"Project {projectId} not found");
            return DeserializeTeamMembers(project.TeamMembers);
        }

        /// <summary>
        /// Takes a row-level lock on the project so concurrent team-member writes can't
        /// clobber each other (the member list is a JSON blob read-modify-written in memory).
        /// </summary>
        private Task LockProjectRowAsync(int projectId) =>
            _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Projects\" WHERE \"Id\" = {projectId} FOR UPDATE");

        public Task<bool> AssignTeamMemberAsync(int projectId, AssignTeamMemberRequestDto dto, string userId) =>
            _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => AssignTeamMemberCoreAsync(projectId, dto, userId));

        private async Task<bool> AssignTeamMemberCoreAsync(int projectId, AssignTeamMemberRequestDto dto, string userId)
        {
            if (dto.UserId <= 0) throw new InvalidOperationException("userId must be greater than 0");

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await LockProjectRowAsync(projectId);

                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                if (project == null) throw new KeyNotFoundException($"Project {projectId} not found");

                var userExists = await _context.Users.AnyAsync(u => u.Id == dto.UserId && !u.IsDeleted);
                if (!userExists) throw new KeyNotFoundException($"User {dto.UserId} not found");

                var members = DeserializeTeamMembers(project.TeamMembers);
                if (members.Contains(dto.UserId))
                {
                    await tx.CommitAsync();
                    return true;
                }
                members.Add(dto.UserId);
                project.TeamMembers = SerializeTeamMembers(members);
                project.ModifiedBy = userId;
                project.ModifiedDate = DateTime.UtcNow;

                await _context.Set<ProjectActivity>().AddAsync(new ProjectActivity
                {
                    ProjectId = projectId,
                    ActionType = "member_added",
                    Description = $"Team member {(string.IsNullOrEmpty(dto.UserName) ? $"#{dto.UserId}" : dto.UserName)} added",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId,
                    RelatedEntityId = dto.UserId,
                    RelatedEntityType = "TeamMember"
                });
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public Task<bool> RemoveTeamMemberAsync(int projectId, int userIdToRemove, string userId) =>
            _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => RemoveTeamMemberCoreAsync(projectId, userIdToRemove, userId));

        private async Task<bool> RemoveTeamMemberCoreAsync(int projectId, int userIdToRemove, string userId)
        {
            if (userIdToRemove <= 0) throw new InvalidOperationException("userId must be greater than 0");

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await LockProjectRowAsync(projectId);

                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                if (project == null) throw new KeyNotFoundException($"Project {projectId} not found");

                var members = DeserializeTeamMembers(project.TeamMembers);
                if (!members.Remove(userIdToRemove))
                {
                    await tx.CommitAsync();
                    return false;
                }
                project.TeamMembers = SerializeTeamMembers(members);
                project.ModifiedBy = userId;
                project.ModifiedDate = DateTime.UtcNow;

                await _context.Set<ProjectActivity>().AddAsync(new ProjectActivity
                {
                    ProjectId = projectId,
                    ActionType = "member_removed",
                    Description = $"Team member #{userIdToRemove} removed",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId,
                    RelatedEntityId = userIdToRemove,
                    RelatedEntityType = "TeamMember"
                });
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }


        private ProjectResponseDto MapToProjectDto(Project project)
        {
            return new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                ContactId = project.ContactId,
                ContactName = project.Contact != null 
                    ? $"{project.Contact.FirstName} {project.Contact.LastName}".Trim() 
                    : null,
                Status = project.Status,
                ProjectKind = project.ProjectKind ?? "client",
                Priority = project.Priority,
                Budget = project.Budget,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                TeamMembers = DeserializeTeamMembers(project.TeamMembers),
                CreatedDate = project.CreatedDate,
                CreatedBy = project.CreatedBy,
                ModifiedDate = project.ModifiedDate,
                ModifiedBy = project.ModifiedBy,
                Columns = new List<ProjectColumnDto>()
            };
        }

        private async Task SetProjectIdForLinkedEntitiesAsync(int projectId, int? offerId, int? saleId, int? serviceOrderId, int? dispatchId)
        {
            if (offerId.HasValue)
            {
                var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == offerId.Value && !o.IsDeleted);
                if (offer != null) offer.ProjectId = projectId;
            }
            if (saleId.HasValue)
            {
                var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == saleId.Value && !s.IsDeleted);
                if (sale != null) sale.ProjectId = projectId;
            }
            if (serviceOrderId.HasValue)
            {
                var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(s => s.Id == serviceOrderId.Value && !s.IsDeleted);
                if (serviceOrder != null) serviceOrder.ProjectId = projectId;
            }
            if (dispatchId.HasValue)
            {
                var dispatch = await _context.Dispatches.FirstOrDefaultAsync(d => d.Id == dispatchId.Value && !d.IsDeleted);
                if (dispatch != null) dispatch.ProjectId = projectId;
            }
            await _context.SaveChangesAsync();
        }

        private async Task SetEntityProjectAsync(string entityType, int entityId, int? projectId, string userId)
        {
            switch (entityType.Trim().ToLowerInvariant())
            {
                case "offer":
                case "offers":
                    var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == entityId && !o.IsDeleted);
                    if (offer == null) throw new KeyNotFoundException($"Offer {entityId} not found");
                    offer.ProjectId = projectId;
                    offer.ModifiedBy = userId;
                    offer.ModifiedDate = DateTime.UtcNow;
                    offer.UpdatedAt = DateTime.UtcNow;
                    break;
                case "sale":
                case "sales":
                    var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == entityId && !s.IsDeleted);
                    if (sale == null) throw new KeyNotFoundException($"Sale {entityId} not found");
                    sale.ProjectId = projectId;
                    sale.ModifiedBy = userId;
                    sale.ModifiedDate = DateTime.UtcNow;
                    sale.UpdatedAt = DateTime.UtcNow;
                    break;
                case "service_order":
                case "serviceorder":
                case "service-orders":
                    var so = await _context.ServiceOrders.FirstOrDefaultAsync(s => s.Id == entityId && !s.IsDeleted);
                    if (so == null) throw new KeyNotFoundException($"Service Order {entityId} not found");
                    so.ProjectId = projectId;
                    so.ModifiedBy = userId;
                    so.ModifiedDate = DateTime.UtcNow;
                    break;
                case "dispatch":
                case "dispatches":
                    var dispatch = await _context.Dispatches.FirstOrDefaultAsync(d => d.Id == entityId && !d.IsDeleted);
                    if (dispatch == null) throw new KeyNotFoundException($"Dispatch {entityId} not found");
                    dispatch.ProjectId = projectId;
                    dispatch.ModifiedBy = userId;
                    dispatch.ModifiedDate = DateTime.UtcNow;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported entityType '{entityType}'");
            }
        }
    }
}
