using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Infrastructure;
using MyApi.Modules.Shared.DTOs;
using MyApi.Modules.Shared.Models;
using System.Text.Json;

namespace MyApi.Modules.Shared.Services
{
    public class SystemLogService : ISystemLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SystemLogService> _logger;
        private readonly ITenantDbContextFactory _dbContextFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SystemLogService(
            ApplicationDbContext context,
            ILogger<SystemLogService> logger,
            ITenantDbContextFactory dbContextFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _dbContextFactory = dbContextFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        private DbSet<SystemLog> SystemLogs => _context.Set<SystemLog>();

        public async Task<SystemLogListResponseDto> GetLogsAsync(SystemLogSearchRequestDto? searchRequest = null)
        {
            // System logs are an audit trail visible to admins regardless of the
            // current tenant context. Bypass the global tenant query filter so a
            // missing or "view-all" tenant context cannot crash the endpoint.
            var query = SystemLogs.AsNoTracking().IgnoreQueryFilters().AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchRequest?.SearchTerm))
            {
                var term = searchRequest.SearchTerm.ToLower();
                query = query.Where(l => 
                    l.Message.ToLower().Contains(term) ||
                    l.Module.ToLower().Contains(term) ||
                    (l.Details != null && l.Details.ToLower().Contains(term)) ||
                    (l.UserName != null && l.UserName.ToLower().Contains(term)) ||
                    (l.EntityType != null && l.EntityType.ToLower().Contains(term))
                );
            }

            if (!string.IsNullOrEmpty(searchRequest?.Level) && searchRequest.Level != "all")
            {
                query = query.Where(l => l.Level == searchRequest.Level);
            }

            if (!string.IsNullOrEmpty(searchRequest?.Module))
            {
                query = query.Where(l => l.Module == searchRequest.Module);
            }

            if (!string.IsNullOrEmpty(searchRequest?.Action))
            {
                query = query.Where(l => l.Action == searchRequest.Action);
            }

            if (!string.IsNullOrEmpty(searchRequest?.UserId))
            {
                query = query.Where(l => l.UserId == searchRequest.UserId);
            }

            if (searchRequest?.StartDate.HasValue == true)
            {
                query = query.Where(l => l.Timestamp >= searchRequest.StartDate.Value);
            }

            if (searchRequest?.EndDate.HasValue == true)
            {
                query = query.Where(l => l.Timestamp <= searchRequest.EndDate.Value);
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Pagination
            var pageNumber = searchRequest?.PageNumber ?? 1;
            var pageSize = searchRequest?.PageSize ?? 50;
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(l => MapToDto(l))
                .ToListAsync();

            return new SystemLogListResponseDto
            {
                Logs = logs,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task<SystemLogDto?> GetLogByIdAsync(int id)
        {
            var log = await SystemLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(l => l.Id == id);
            return log != null ? MapToDto(log) : null;
        }

        public async Task<SystemLogDto> CreateLogAsync(CreateSystemLogRequestDto createDto, string? ipAddress = null)
        {
            var log = new SystemLog
            {
                Timestamp = DateTime.UtcNow,
                Level = ValidateLevel(createDto.Level),
                Message = createDto.Message,
                Module = createDto.Module,
                Action = ValidateAction(createDto.Action),
                UserId = createDto.UserId,
                UserName = createDto.UserName,
                EntityType = createDto.EntityType,
                EntityId = createDto.EntityId,
                Details = createDto.Details,
                IpAddress = ipAddress ?? createDto.IpAddress,
                UserAgent = createDto.UserAgent,
                Metadata = createDto.Metadata != null ? JsonSerializer.Serialize(createDto.Metadata) : null
            };

            await PersistLogResilientlyAsync(log, "CreateLogAsync");
            return MapToDto(log);
        }

        public async Task<SystemLogStatisticsDto> GetStatisticsAsync()
        {
            var now = DateTime.UtcNow;
            var last24Hours = now.AddHours(-24);
            var last7Days = now.AddDays(-7);

            var stats = await SystemLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(l => l.Timestamp >= last7Days)
                .GroupBy(l => 1)
                .Select(g => new SystemLogStatisticsDto
                {
                    TotalLogs = g.Count(),
                    InfoCount = g.Count(l => l.Level == "info"),
                    WarningCount = g.Count(l => l.Level == "warning"),
                    ErrorCount = g.Count(l => l.Level == "error"),
                    SuccessCount = g.Count(l => l.Level == "success"),
                    Last24Hours = g.Count(l => l.Timestamp >= last24Hours),
                    Last7Days = g.Count()
                })
                .FirstOrDefaultAsync();

            return stats ?? new SystemLogStatisticsDto();
        }

        public async Task<List<string>> GetModulesAsync()
        {
            return await SystemLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Select(l => l.Module)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();
        }

        public async Task<CleanupResultDto> CleanupOldLogsAsync(int daysOld = 7)
        {
            List<SystemLog> logsToDelete;
            
            if (daysOld == 0)
            {
                // Delete ALL logs
                logsToDelete = await SystemLogs.IgnoreQueryFilters().ToListAsync();
            }
            else
            {
                // Delete logs older than specified days
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
                logsToDelete = await SystemLogs
                    .IgnoreQueryFilters()
                    .Where(l => l.Timestamp < cutoffDate)
                    .ToListAsync();
            }

            var count = logsToDelete.Count;
            
            if (count > 0)
            {
                SystemLogs.RemoveRange(logsToDelete);
                await _context.SaveChangesAsync();
            }

            var message = daysOld == 0 
                ? $"Successfully deleted all {count} logs"
                : $"Successfully deleted {count} logs older than {daysOld} days";

            _logger.LogInformation("Cleaned up {Count} system logs (daysOld={Days})", count, daysOld);

            return new CleanupResultDto
            {
                DeletedCount = count,
                Message = message
            };
        }

        // Quick logging methods
        public Task LogInfoAsync(string message, string module, string action = "other", string? userId = null, string? userName = null, string? entityType = null, string? entityId = null, string? details = null)
            => CreateQuickLogAsync("info", message, module, action, userId, userName, entityType, entityId, details);

        public Task LogWarningAsync(string message, string module, string action = "other", string? userId = null, string? userName = null, string? entityType = null, string? entityId = null, string? details = null)
            => CreateQuickLogAsync("warning", message, module, action, userId, userName, entityType, entityId, details);

        public Task LogErrorAsync(string message, string module, string action = "other", string? userId = null, string? userName = null, string? entityType = null, string? entityId = null, string? details = null)
            => CreateQuickLogAsync("error", message, module, action, userId, userName, entityType, entityId, details);

        public Task LogSuccessAsync(string message, string module, string action = "other", string? userId = null, string? userName = null, string? entityType = null, string? entityId = null, string? details = null)
            => CreateQuickLogAsync("success", message, module, action, userId, userName, entityType, entityId, details);

        private async Task CreateQuickLogAsync(string level, string message, string module, string action, string? userId, string? userName, string? entityType, string? entityId, string? details)
        {
            var log = new SystemLog
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                Module = module,
                Action = action,
                UserId = userId,
                UserName = userName,
                EntityType = entityType,
                EntityId = entityId,
                Details = details
            };

            await PersistLogResilientlyAsync(log, "CreateQuickLogAsync");
        }

        /// <summary>
        /// Persist a SystemLog on a DEDICATED, isolated DbContext.
        ///
        /// Logging must never share the request-scoped DbContext: if a caller's
        /// SaveChanges has already failed (e.g. a 22001 value-too-long on some other
        /// entity), that bad entity stays tracked in the Added state. Re-using the
        /// same context here would re-attempt the failed insert on every log write,
        /// so the log save fails for an unrelated reason and the audit entry is lost.
        ///
        /// We therefore spin up a fresh context from the tenant factory, write only
        /// the log, and dispose it. The primary attempt uses the current tenant; if
        /// that fails we retry once against the system/global tenant (0). Everything
        /// is wrapped so logging can never throw back to the caller.
        /// </summary>
        private async Task PersistLogResilientlyAsync(SystemLog log, string source)
        {
            var tenant = _httpContextAccessor.HttpContext?.Items["Tenant"] as string;

            int currentTenantId;
            try { currentTenantId = _context.GetTenantId(); }
            catch { currentTenantId = 0; }
            // view-all sentinel (-1) is not a writable tenant — fall straight to 0.
            if (currentTenantId < 0) currentTenantId = 0;

            if (await TryPersistAsync(log, tenant, currentTenantId, source, isFallback: false))
                return;

            // Fallback: system/global tenant (0).
            await TryPersistAsync(log, tenant, 0, source, isFallback: true);
        }

        private async Task<bool> TryPersistAsync(SystemLog log, string? tenant, int tenantId, string source, bool isFallback)
        {
            try
            {
                await using var ctx = _dbContextFactory.CreateDbContext(tenant);
                ctx.SetTenantId(tenantId);

                var entry = new SystemLog
                {
                    TenantId = tenantId,
                    Timestamp = log.Timestamp == default ? DateTime.UtcNow : log.Timestamp,
                    Level = NormalizeLevel(log.Level),
                    Message = string.IsNullOrEmpty(log.Message)
                        ? "(system log written without tenant context)"
                        : log.Message,
                    Module = Cap(string.IsNullOrEmpty(log.Module) ? "System" : log.Module, 100),
                    Action = NormalizeAction(log.Action),
                    UserId = CapOptional(log.UserId, 100),
                    UserName = CapOptional(log.UserName, 200),
                    EntityType = CapOptional(log.EntityType, 100),
                    EntityId = CapOptional(log.EntityId, 100),
                    Details = log.Details,
                    IpAddress = CapOptional(log.IpAddress, 45),
                    UserAgent = log.UserAgent,
                    Metadata = log.Metadata
                };

                ctx.Set<SystemLog>().Add(entry);
                await ctx.SaveChangesAsync();

                // Mirror the persisted Id back so callers (CreateLogAsync) get a usable DTO
                log.Id = entry.Id;
                log.TenantId = entry.TenantId;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    isFallback
                        ? "SystemLog fallback persist also failed in {Source}; dropping log entry"
                        : "SystemLog primary persist failed in {Source}; retrying with fallback tenant",
                    source);
                return false;
            }
        }

        // Trim a value to the backing column width so an over-length field can never
        // throw a 22001 and take the whole log entry down with it.
        private static string Cap(string value, int max)
            => value.Length > max ? value.Substring(0, max) : value;

        private static string? CapOptional(string? value, int max)
            => value != null && value.Length > max ? value.Substring(0, max) : value;

        // The SystemLogs table enforces a CHECK constraint that limits Action to a
        // fixed vocabulary. Anything else (e.g. "forgot_password", "send_email")
        // would blow up the whole insert with 23514. Normalize to the allowed set
        // and fall back to "other" for unknown values.
        private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "create", "read", "update", "delete", "login", "logout", "export", "import", "other"
        };

        private static readonly HashSet<string> AllowedLevels = new(StringComparer.OrdinalIgnoreCase)
        {
            "info", "warning", "error", "success"
        };

        private static string NormalizeAction(string? action)
        {
            if (string.IsNullOrWhiteSpace(action)) return "other";
            var trimmed = action.Trim().ToLowerInvariant();
            if (AllowedActions.Contains(trimmed)) return trimmed;

            // Best-effort mapping of common verbs onto the allowed vocabulary.
            return trimmed switch
            {
                "add" or "insert" or "register" or "signup" or "sign_up" => "create",
                "view" or "list" or "get" or "fetch" or "search" => "read",
                "edit" or "modify" or "patch" or "reset" or "reset_password" or "forgot_password" or "change_password" => "update",
                "remove" or "destroy" or "purge" or "cleanup" => "delete",
                "signin" or "sign_in" or "authenticate" => "login",
                "signout" or "sign_out" => "logout",
                "send" or "email" or "send_email" or "notify" or "notification" => "other",
                _ => "other"
            };
        }

        private static string NormalizeLevel(string? level)
        {
            if (string.IsNullOrWhiteSpace(level)) return "error";
            var trimmed = level.Trim().ToLowerInvariant();
            if (AllowedLevels.Contains(trimmed)) return trimmed;
            return trimmed switch
            {
                "warn" => "warning",
                "err" or "fail" or "fatal" or "critical" => "error",
                "ok" or "done" => "success",
                _ => "info"
            };
        }


        private static SystemLogDto MapToDto(SystemLog log)
        {
            return new SystemLogDto
            {
                Id = log.Id,
                Timestamp = log.Timestamp,
                Level = log.Level,
                Message = log.Message,
                Module = log.Module,
                Action = log.Action,
                UserId = log.UserId,
                UserName = log.UserName,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                Details = log.Details,
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                Metadata = !string.IsNullOrEmpty(log.Metadata) 
                    ? JsonSerializer.Deserialize<object>(log.Metadata) 
                    : null
            };
        }

        private static string ValidateLevel(string level)
        {
            var validLevels = new[] { "info", "warning", "error", "success" };
            return validLevels.Contains(level.ToLower()) ? level.ToLower() : "info";
        }

        private static string ValidateAction(string action)
        {
            var validActions = new[] { "create", "read", "update", "delete", "login", "logout", "export", "import", "other" };
            return validActions.Contains(action.ToLower()) ? action.ToLower() : "other";
        }
    }
}
