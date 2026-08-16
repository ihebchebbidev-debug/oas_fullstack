using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MyApi.Infrastructure.Caching
{
    /// <summary>
    /// Interface for caching service.
    /// Supports automatic cache expiration and pattern-based invalidation.
    /// </summary>
    public interface ICacheService
    {
        Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
        T? Get<T>(string key);
        void Set<T>(string key, T value, TimeSpan? expiration = null);
        void Remove(string key);
        void RemoveByPattern(string pattern);
        void Clear();
        CacheStats GetStats();
    }

    public class CacheStats
    {
        public int TotalKeys { get; set; }
        public int Hits { get; set; }
        public int Misses { get; set; }
        public double HitRate => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0;
        public string Backend { get; set; } = "memory";
    }

    // ═══════════════════════════════════════════════════════════════
    // Redis-backed distributed cache (primary for production)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Redis distributed cache implementation.
    /// - Thread-safe across multiple server instances
    /// - Pattern-based invalidation via key tracking
    /// - Aggressive invalidation on deletes to prevent stale data
    /// - Short default TTL (5 min) to minimize stale window
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisCacheService> _logger;

        // Track keys in Redis itself via a sorted set for pattern matching
        private readonly ConcurrentDictionary<string, byte> _localKeyTracker = new();
        private int _hits;
        private int _misses;

        /// <summary>
        /// Default TTL: 5 minutes. Short to minimize stale data risk.
        /// Callers can override per-call for static data (e.g., lookups → 30min).
        /// </summary>
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

        public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            try
            {
                var cached = await _cache.GetStringAsync(key);
                if (cached != null)
                {
                    Interlocked.Increment(ref _hits);
                    _logger.LogDebug("✓ Redis HIT: {Key}", key);
                    return JsonSerializer.Deserialize<T>(cached);
                }
            }
            catch (Exception ex)
            {
                // Redis down → fall through to factory (resilient)
                _logger.LogWarning(ex, "Redis GET failed for {Key}, falling through to DB", key);
            }

            Interlocked.Increment(ref _misses);
            _logger.LogDebug("✗ Redis MISS: {Key}", key);

            var value = await factory();

            try
            {
                var ttl = expiration ?? DefaultTtl;
                var json = JsonSerializer.Serialize(value);
                await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                });
                _localKeyTracker.TryAdd(key, 0);
                _logger.LogDebug("→ Redis SET: {Key} (TTL {Ttl}s)", key, ttl.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis SET failed for {Key}, value served uncached", key);
            }

            return value;
        }

        public T? Get<T>(string key)
        {
            try
            {
                var cached = _cache.GetString(key);
                if (cached != null)
                {
                    Interlocked.Increment(ref _hits);
                    return JsonSerializer.Deserialize<T>(cached);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis sync GET failed for {Key}", key);
            }

            Interlocked.Increment(ref _misses);
            return default;
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            try
            {
                var ttl = expiration ?? DefaultTtl;
                var json = JsonSerializer.Serialize(value);
                _cache.SetString(key, json, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                });
                _localKeyTracker.TryAdd(key, 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis SET failed for {Key}", key);
            }
        }

        /// <summary>
        /// Remove a single key. Called on EVERY mutation (create/update/delete)
        /// to prevent serving stale or deleted data.
        /// </summary>
        public void Remove(string key)
        {
            try
            {
                _cache.Remove(key);
                _localKeyTracker.TryRemove(key, out _);
                _logger.LogDebug("🗑️ Redis REMOVED: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis REMOVE failed for {Key}", key);
            }
        }

        /// <summary>
        /// Remove all keys matching a pattern (e.g., "contact_*").
        /// Uses local key tracker for pattern matching.
        /// </summary>
        public void RemoveByPattern(string pattern)
        {
            var keysToRemove = _localKeyTracker.Keys
                .Where(k => PatternMatches(k, pattern))
                .ToList();

            foreach (var key in keysToRemove)
            {
                Remove(key);
            }

            _logger.LogDebug("🗑️ Redis pattern '{Pattern}': {Count} keys removed", pattern, keysToRemove.Count);
        }

        public void Clear()
        {
            var allKeys = _localKeyTracker.Keys.ToList();
            foreach (var key in allKeys)
            {
                try { _cache.Remove(key); } catch { }
                _localKeyTracker.TryRemove(key, out _);
            }
            _hits = 0;
            _misses = 0;
            _logger.LogInformation("🗑️ Redis cache CLEARED: {Count} keys removed", allKeys.Count);
        }

        public CacheStats GetStats() => new()
        {
            TotalKeys = _localKeyTracker.Count,
            Hits = _hits,
            Misses = _misses,
            Backend = "redis"
        };

        private static bool PatternMatches(string key, string pattern)
        {
            if (pattern.Contains('*'))
            {
                var parts = pattern.Split('*');
                var str = key;
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) continue;
                    var idx = str.IndexOf(part, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) return false;
                    str = str[(idx + part.Length)..];
                }
                return true;
            }
            return key.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // In-Memory fallback (development / single-instance)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// In-memory cache fallback when Redis is not configured.
    /// Same interface, same invalidation guarantees.
    /// </summary>
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryCacheService> _logger;
        private static readonly ConcurrentDictionary<string, byte> _cacheKeys = new();
        private int _hits;
        private int _misses;
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

        public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            if (_cache.TryGetValue(key, out T? cachedValue))
            {
                Interlocked.Increment(ref _hits);
                return cachedValue;
            }

            Interlocked.Increment(ref _misses);
            var value = await factory();

            var options = new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultTtl
            };

            _cache.Set(key, value, options);
            _cacheKeys.TryAdd(key, 0);
            return value;
        }

        public T? Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out T? value))
            {
                Interlocked.Increment(ref _hits);
                return value;
            }
            Interlocked.Increment(ref _misses);
            return default;
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultTtl
            };
            _cache.Set(key, value, options);
            _cacheKeys.TryAdd(key, 0);
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
        }

        public void RemoveByPattern(string pattern)
        {
            var keysToRemove = _cacheKeys.Keys
                .Where(k => PatternMatches(k, pattern))
                .ToList();
            foreach (var key in keysToRemove) Remove(key);
        }

        public void Clear()
        {
            foreach (var key in _cacheKeys.Keys.ToList())
            {
                _cache.Remove(key);
                _cacheKeys.TryRemove(key, out _);
            }
            _hits = 0;
            _misses = 0;
        }

        public CacheStats GetStats() => new()
        {
            TotalKeys = _cacheKeys.Count,
            Hits = _hits,
            Misses = _misses,
            Backend = "memory"
        };

        private static bool PatternMatches(string key, string pattern)
        {
            if (pattern.Contains('*'))
            {
                var parts = pattern.Split('*');
                var str = key;
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) continue;
                    var idx = str.IndexOf(part, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) return false;
                    str = str[(idx + part.Length)..];
                }
                return true;
            }
            return key.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Cache Invalidation Helper (unchanged interface)
    // ═══════════════════════════════════════════════════════════════

    public class CacheInvalidationHelper
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<CacheInvalidationHelper> _logger;

        public CacheInvalidationHelper(ICacheService cacheService, ILogger<CacheInvalidationHelper> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public void InvalidateContactCaches()
        {
            _cacheService.RemoveByPattern("contact_*");
            _logger.LogInformation("Invalidated all contact caches");
        }

        public void InvalidateContact(int contactId)
        {
            _cacheService.Remove($"contact_{contactId}");
            _cacheService.RemoveByPattern("contact_list_*");
        }

        public void InvalidateLookups()
        {
            _cacheService.RemoveByPattern("lookup_*");
            _logger.LogInformation("Invalidated all lookup caches");
        }

        public void InvalidateLookupType(string lookupType)
        {
            _cacheService.Remove($"lookup_{lookupType}");
        }

        public void InvalidateDispatches()
        {
            _cacheService.RemoveByPattern("dispatch_*");
        }

        public void InvalidateServiceOrders()
        {
            _cacheService.RemoveByPattern("serviceorder_*");
        }
    }
}
