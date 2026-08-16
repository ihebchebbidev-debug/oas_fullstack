using MyApi.Data;
using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Models;
using Microsoft.EntityFrameworkCore;

namespace MyApi.Modules.Projects.Services
{
    public class ProjectColumnService : IProjectColumnService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProjectColumnService> _logger;

        public ProjectColumnService(ApplicationDbContext context, ILogger<ProjectColumnService> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static ProjectColumnResponseDto Map(ProjectColumn c, int taskCount = 0) => new ProjectColumnResponseDto
        {
            Id = c.Id,
            ProjectId = c.ProjectId,
            Name = c.Name,
            DisplayOrder = c.DisplayOrder,
            Color = c.Color,
            TaskCount = taskCount
        };

        public async Task<ProjectColumnListResponseDto> GetProjectColumnsAsync(int projectId)
        {
            var cols = await _context.Set<ProjectColumn>()
                .AsNoTracking()
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            return new ProjectColumnListResponseDto
            {
                Columns = cols.Select(c => Map(c)).ToList(),
                TotalCount = cols.Count
            };
        }

        public async Task<ProjectColumnResponseDto?> GetColumnByIdAsync(int id)
        {
            var c = await _context.Set<ProjectColumn>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return c == null ? null : Map(c);
        }

        public async Task<ProjectColumnResponseDto> CreateColumnAsync(CreateProjectColumnRequestDto createDto, string createdByUser)
        {
            var col = new ProjectColumn
            {
                ProjectId = createDto.ProjectId,
                Name = createDto.Name,
                DisplayOrder = createDto.DisplayOrder > 0
                    ? createDto.DisplayOrder
                    : await GetNextColumnDisplayOrderAsync(createDto.ProjectId),
                Color = createDto.Color
            };
            _context.Set<ProjectColumn>().Add(col);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Column {ColumnId} created on project {ProjectId} by {User}", col.Id, col.ProjectId, createdByUser);
            return Map(col);
        }

        public async Task<ProjectColumnResponseDto?> UpdateColumnAsync(int id, UpdateProjectColumnRequestDto updateDto, string modifiedByUser)
        {
            var col = await _context.Set<ProjectColumn>().FirstOrDefaultAsync(c => c.Id == id);
            if (col == null) return null;
            if (!string.IsNullOrEmpty(updateDto.Name)) col.Name = updateDto.Name;
            if (updateDto.DisplayOrder.HasValue) col.DisplayOrder = updateDto.DisplayOrder.Value;
            if (updateDto.Color != null) col.Color = updateDto.Color;
            await _context.SaveChangesAsync();
            return Map(col);
        }

        public async Task<bool> DeleteColumnAsync(int id, int? moveTasksToColumnId, string deletedByUser)
        {
            var col = await _context.Set<ProjectColumn>().FirstOrDefaultAsync(c => c.Id == id);
            if (col == null) return false;
            _context.Set<ProjectColumn>().Remove(col);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReorderColumnsAsync(int projectId, ReorderProjectColumnsRequestDto reorderDto, string updatedByUser)
        {
            var ids = reorderDto.Columns.Select(c => c.Id).ToList();
            var cols = await _context.Set<ProjectColumn>()
                .Where(c => c.ProjectId == projectId && ids.Contains(c.Id))
                .ToListAsync();
            foreach (var c in cols)
            {
                var target = reorderDto.Columns.FirstOrDefault(p => p.Id == c.Id);
                if (target != null) c.DisplayOrder = target.DisplayOrder;
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetNextColumnDisplayOrderAsync(int projectId)
        {
            var max = await _context.Set<ProjectColumn>()
                .Where(c => c.ProjectId == projectId)
                .Select(c => (int?)c.DisplayOrder)
                .MaxAsync() ?? 0;
            return max + 1;
        }

        public Task<bool> ColumnExistsAsync(int id) =>
            _context.Set<ProjectColumn>().AnyAsync(c => c.Id == id);

        public Task<bool> ColumnBelongsToProjectAsync(int columnId, int projectId) =>
            _context.Set<ProjectColumn>().AnyAsync(c => c.Id == columnId && c.ProjectId == projectId);

        public Task<bool> UserCanManageProjectColumnsAsync(int projectId, int userId) =>
            Task.FromResult(true);

        public Task<bool> CanDeleteColumnAsync(int columnId) => Task.FromResult(true);

        public Task<int> GetColumnTaskCountAsync(int columnId) => Task.FromResult(0);

        public async Task<bool> CreateDefaultColumnsAsync(int projectId, string createdByUser)
        {
            var existing = await _context.Set<ProjectColumn>().AnyAsync(c => c.ProjectId == projectId);
            if (existing) return true;
            var defaults = new[]
            {
                new ProjectColumn { ProjectId = projectId, Name = "To Do", DisplayOrder = 1, Color = "#94a3b8" },
                new ProjectColumn { ProjectId = projectId, Name = "In Progress", DisplayOrder = 2, Color = "#3b82f6" },
                new ProjectColumn { ProjectId = projectId, Name = "Done", DisplayOrder = 3, Color = "#10b981" }
            };
            _context.Set<ProjectColumn>().AddRange(defaults);
            await _context.SaveChangesAsync();
            return true;
        }

        public Task<List<ProjectColumnResponseDto>> GetDefaultColumnTemplatesAsync() =>
            Task.FromResult(new List<ProjectColumnResponseDto>
            {
                new() { Name = "To Do", DisplayOrder = 1, Color = "#94a3b8" },
                new() { Name = "In Progress", DisplayOrder = 2, Color = "#3b82f6" },
                new() { Name = "Done", DisplayOrder = 3, Color = "#10b981" }
            });

        public async Task<bool> BulkDeleteColumnsAsync(BulkDeleteProjectColumnsDto bulkDeleteDto, string deletedByUser)
        {
            var cols = await _context.Set<ProjectColumn>()
                .Where(c => bulkDeleteDto.ColumnIds.Contains(c.Id))
                .ToListAsync();
            if (cols.Count == 0) return false;
            _context.Set<ProjectColumn>().RemoveRange(cols);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> BulkUpdateColumnColorsAsync(Dictionary<int, string> columnColors, string updatedByUser)
        {
            var ids = columnColors.Keys.ToList();
            var cols = await _context.Set<ProjectColumn>().Where(c => ids.Contains(c.Id)).ToListAsync();
            foreach (var c in cols)
            {
                if (columnColors.TryGetValue(c.Id, out var color)) c.Color = color;
            }
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
