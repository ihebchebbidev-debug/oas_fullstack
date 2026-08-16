using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.Sync.Models;

namespace MyApi.Modules.Sync.Services
{
    /// <summary>
    /// Service for logging offline sync failures, performance, and token refresh events
    /// </summary>
    public interface ISyncLoggingService
    {
        Task LogSyncFailureAsync(string deviceId, int? userId, string opId, string? entityType, 
            string? status, string? errorMessage, int? httpStatus, string? httpBody, 
            string? endpoint, string? method, string? tenant);

        Task LogSyncPerformanceAsync(string deviceId, int? userId, long syncDuration, 
            int attempted, int succeeded, int failed, long? bytesSent, long? bytesReceived, 
            string? networkType, string? tenant);

        Task LogTokenRefreshAsync(int userId, string? reason, bool success, string? errorMessage, 
            string? tenant, string? deviceId);

        Task MarkFailureAsResolvedAsync(int failureId);

        Task<List<SyncFailureLog>> GetRecentFailuresAsync(string deviceId, int hours = 24);

        Task<List<SyncPerformanceLog>> GetPerformanceStatsAsync(string deviceId, int hours = 24);

        Task CleanupOldLogsAsync(int daysToKeep = 30);
    }

    /// <summary>
    /// Implementation of sync logging service
    /// </summary>
    public class SyncLoggingService : ISyncLoggingService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<SyncLoggingService> _logger;

        public SyncLoggingService(ApplicationDbContext dbContext, ILogger<SyncLoggingService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Log a sync failure for debugging and monitoring
        /// </summary>
        public async Task LogSyncFailureAsync(string deviceId, int? userId, string opId, string? entityType,
            string? status, string? errorMessage, int? httpStatus, string? httpBody,
            string? endpoint, string? method, string? tenant)
        {
            try
            {
                var failure = new SyncFailureLog
                {
                    DeviceId = deviceId,
                    UserId = userId,
                    OpId = opId,
                    EntityType = entityType,
                    Status = status,
                    ErrorMessage = errorMessage,
                    HttpStatus = httpStatus,
                    HttpBody = httpBody?.Length > 5000 ? httpBody.Substring(0, 5000) : httpBody, // Truncate very large responses
                    Endpoint = endpoint,
                    Method = method,
                    Timestamp = DateTime.UtcNow,
                    Tenant = tenant,
                    Resolved = false
                };

                _dbContext.Set<SyncFailureLog>().Add(failure);
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug(
                    "Logged sync failure: Device={DeviceId}, User={UserId}, Op={OpId}, Entity={EntityType}, Status={HttpStatus}",
                    deviceId, userId, opId, entityType, httpStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log sync failure for device {DeviceId}", deviceId);
                // Don't throw - logging failures should not break sync
            }
        }

        /// <summary>
        /// Log sync performance metrics for analytics
        /// </summary>
        public async Task LogSyncPerformanceAsync(string deviceId, int? userId, long syncDuration,
            int attempted, int succeeded, int failed, long? bytesSent, long? bytesReceived,
            string? networkType, string? tenant)
        {
            try
            {
                var performance = new SyncPerformanceLog
                {
                    DeviceId = deviceId,
                    UserId = userId,
                    SyncDuration = syncDuration,
                    OperationsAttempted = attempted,
                    OperationsSucceeded = succeeded,
                    OperationsFailed = failed,
                    BytesSent = bytesSent,
                    BytesReceived = bytesReceived,
                    Timestamp = DateTime.UtcNow,
                    NetworkType = networkType,
                    Tenant = tenant
                };

                _dbContext.Set<SyncPerformanceLog>().Add(performance);
                await _dbContext.SaveChangesAsync();

                var successRate = attempted > 0 ? (100.0 * succeeded / attempted) : 0;
                _logger.LogDebug(
                    "Logged sync performance: Device={DeviceId}, Duration={Duration}ms, Success={Succeeded}/{Attempted} ({SuccessRate:F1}%)",
                    deviceId, syncDuration, succeeded, attempted, successRate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log sync performance for device {DeviceId}", deviceId);
                // Don't throw - logging failures should not break sync
            }
        }

        /// <summary>
        /// Log token refresh attempts for auth troubleshooting
        /// </summary>
        public async Task LogTokenRefreshAsync(int userId, string? reason, bool success,
            string? errorMessage, string? tenant, string? deviceId)
        {
            try
            {
                var tokenLog = new TokenRefreshLog
                {
                    UserId = userId,
                    Reason = reason,
                    Success = success,
                    ErrorMessage = errorMessage,
                    Timestamp = DateTime.UtcNow,
                    Tenant = tenant,
                    DeviceId = deviceId
                };

                _dbContext.Set<TokenRefreshLog>().Add(tokenLog);
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug(
                    "Logged token refresh: User={UserId}, Reason={Reason}, Success={Success}",
                    userId, reason, success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log token refresh for user {UserId}", userId);
                // Don't throw - logging failures should not break auth
            }
        }

        /// <summary>
        /// Mark a sync failure as resolved
        /// </summary>
        public async Task MarkFailureAsResolvedAsync(int failureId)
        {
            try
            {
                var failure = await _dbContext.Set<SyncFailureLog>()
                    .FirstOrDefaultAsync(f => f.Id == failureId);

                if (failure != null)
                {
                    failure.Resolved = true;
                    failure.ResolvedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    _logger.LogDebug("Marked sync failure {FailureId} as resolved", failureId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark failure {FailureId} as resolved", failureId);
                // Don't throw
            }
        }

        /// <summary>
        /// Get recent sync failures for a device
        /// </summary>
        public async Task<List<SyncFailureLog>> GetRecentFailuresAsync(string deviceId, int hours = 24)
        {
            try
            {
                var threshold = DateTime.UtcNow.AddHours(-hours);
                return await _dbContext.Set<SyncFailureLog>()
                    .Where(f => f.DeviceId == deviceId && f.Timestamp > threshold && !f.Resolved)
                    .OrderByDescending(f => f.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent failures for device {DeviceId}", deviceId);
                return new List<SyncFailureLog>();
            }
        }

        /// <summary>
        /// Get performance stats for a device
        /// </summary>
        public async Task<List<SyncPerformanceLog>> GetPerformanceStatsAsync(string deviceId, int hours = 24)
        {
            try
            {
                var threshold = DateTime.UtcNow.AddHours(-hours);
                return await _dbContext.Set<SyncPerformanceLog>()
                    .Where(p => p.DeviceId == deviceId && p.Timestamp > threshold)
                    .OrderByDescending(p => p.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get performance stats for device {DeviceId}", deviceId);
                return new List<SyncPerformanceLog>();
            }
        }

        /// <summary>
        /// Cleanup old logs (typically called as scheduled job)
        /// </summary>
        public async Task CleanupOldLogsAsync(int daysToKeep = 30)
        {
            try
            {
                var threshold = DateTime.UtcNow.AddDays(-daysToKeep);

                // Delete old resolved failures
                var oldFailures = _dbContext.Set<SyncFailureLog>()
                    .Where(f => f.Timestamp < threshold && f.Resolved);
                await oldFailures.ExecuteDeleteAsync();

                // Delete old performance logs (keep longer for statistics)
                var oldPerformance = _dbContext.Set<SyncPerformanceLog>()
                    .Where(p => p.Timestamp < threshold.AddDays(-daysToKeep)); // double the retention
                await oldPerformance.ExecuteDeleteAsync();

                // Delete old token refresh logs
                var oldTokens = _dbContext.Set<TokenRefreshLog>()
                    .Where(t => t.Timestamp < threshold);
                await oldTokens.ExecuteDeleteAsync();

                _logger.LogInformation(
                    "Cleaned up sync logs older than {DaysToKeep} days",
                    daysToKeep);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old sync logs");
                // Don't throw - cleanup failures should not break operations
            }
        }
    }
}
