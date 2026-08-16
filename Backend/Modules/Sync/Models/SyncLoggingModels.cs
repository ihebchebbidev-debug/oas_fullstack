using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApi.Modules.Sync.Models
{
    /// <summary>
    /// Tracks offline sync failures for debugging and monitoring
    /// </summary>
    [Table("SyncFailureLog")]
    public class SyncFailureLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Device identifier (e.g., browser fingerprint or device ID)
        /// </summary>
        [Required]
        [StringLength(255)]
        public string DeviceId { get; set; } = null!;

        /// <summary>
        /// User ID if known (null for auth failures)
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// Operation ID from offline queue
        /// </summary>
        [Required]
        [StringLength(255)]
        public string OpId { get; set; } = null!;

        /// <summary>
        /// Entity type (support_ticket, contact, etc.)
        /// </summary>
        [StringLength(100)]
        public string? EntityType { get; set; }

        /// <summary>
        /// Sync operation status (pending, queued, syncing, failed, blocked)
        /// </summary>
        [StringLength(50)]
        public string? Status { get; set; }

        /// <summary>
        /// Error message from sync failure
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// HTTP status code (403, 401, 500, etc.)
        /// </summary>
        public int? HttpStatus { get; set; }

        /// <summary>
        /// HTTP response body for debugging
        /// </summary>
        public string? HttpBody { get; set; }

        /// <summary>
        /// Endpoint that failed (e.g., /api/sync/push)
        /// </summary>
        [StringLength(500)]
        public string? Endpoint { get; set; }

        /// <summary>
        /// HTTP method (POST, PUT, DELETE)
        /// </summary>
        [StringLength(10)]
        public string? Method { get; set; }

        /// <summary>
        /// When the failure occurred
        /// </summary>
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this failure was resolved/recovered
        /// </summary>
        public bool Resolved { get; set; }

        /// <summary>
        /// When it was resolved (if applicable)
        /// </summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Tenant identifier
        /// </summary>
        [StringLength(255)]
        public string? Tenant { get; set; }
    }

    /// <summary>
    /// Tracks offline sync performance metrics for analytics
    /// </summary>
    [Table("SyncPerformanceLog")]
    public class SyncPerformanceLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Device identifier (e.g., browser fingerprint or device ID)
        /// </summary>
        [Required]
        [StringLength(255)]
        public string DeviceId { get; set; } = null!;

        /// <summary>
        /// User ID
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// Total sync operation duration in milliseconds
        /// </summary>
        [Required]
        public long SyncDuration { get; set; }

        /// <summary>
        /// Total operations in this sync batch
        /// </summary>
        [Required]
        public int OperationsAttempted { get; set; }

        /// <summary>
        /// Successfully processed operations
        /// </summary>
        [Required]
        public int OperationsSucceeded { get; set; }

        /// <summary>
        /// Failed operations
        /// </summary>
        [Required]
        public int OperationsFailed { get; set; }

        /// <summary>
        /// Bytes sent to server
        /// </summary>
        public long? BytesSent { get; set; }

        /// <summary>
        /// Bytes received from server
        /// </summary>
        public long? BytesReceived { get; set; }

        /// <summary>
        /// When sync occurred
        /// </summary>
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tenant identifier
        /// </summary>
        [StringLength(255)]
        public string? Tenant { get; set; }

        /// <summary>
        /// Network type (4G, 5G, WiFi, Unknown)
        /// </summary>
        [StringLength(50)]
        public string? NetworkType { get; set; }

        /// <summary>
        /// Calculate success rate as percentage
        /// </summary>
        public double SuccessRate => OperationsAttempted > 0
            ? Math.Round(100.0 * OperationsSucceeded / OperationsAttempted, 2)
            : 0.0;
    }

    /// <summary>
    /// Logs token refresh attempts for auth troubleshooting
    /// </summary>
    [Table("TokenRefreshLog")]
    public class TokenRefreshLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// User ID
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Reason for refresh (sync_pre_check, 403_error, expiry_detected, etc.)
        /// </summary>
        [StringLength(100)]
        public string? Reason { get; set; }

        /// <summary>
        /// Whether refresh was successful
        /// </summary>
        [Required]
        public bool Success { get; set; }

        /// <summary>
        /// Error message if refresh failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// When the refresh attempt occurred
        /// </summary>
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tenant identifier
        /// </summary>
        [StringLength(255)]
        public string? Tenant { get; set; }

        /// <summary>
        /// Device identifier
        /// </summary>
        [StringLength(255)]
        public string? DeviceId { get; set; }
    }
}
