using System.Collections.Concurrent;

namespace APPID.Services;

/// <summary>
///     In-memory cache for Steam API responses to reduce API calls and improve performance.
/// </summary>
public interface ISteamCacheManager
{
    /// <summary>
    ///     Gets a cached value by key
    /// </summary>
    T? Get<T>(string key) where T : class;

    /// <summary>
    ///     Sets a value in cache with default TTL
    /// </summary>
    void Set<T>(string key, T value) where T : class;

    /// <summary>
    ///     Sets a value in cache with custom TTL
    /// </summary>
    void Set<T>(string key, T value, TimeSpan ttl) where T : class;

    /// <summary>
    ///     Removes a value from cache
    /// </summary>
    void Remove(string key);

    /// <summary>
    ///     Clears all cached values
    /// </summary>
    void Clear();

    /// <summary>
    ///     Checks if a key exists and is not expired
    /// </summary>
    bool Contains(string key);
}

/// <summary>
///     Implementation of in-memory cache for Steam API responses.
///     Thread-safe and supports automatic expiration.
/// </summary>
public sealed class SteamCacheManager : ISteamCacheManager
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultTtl;

    private sealed class CacheEntry
    {
        public object Value { get; }
        public DateTime ExpiresAt { get; }

        public CacheEntry(object value, TimeSpan ttl)
        {
            Value = value;
            ExpiresAt = DateTime.UtcNow.Add(ttl);
        }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    public SteamCacheManager(TimeSpan? defaultTtl = null)
    {
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);

        // Start background cleanup task
        _ = Task.Run(CleanupExpiredEntriesAsync);
    }

    public T? Get<T>(string key) where T : class
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired)
            {
                // Remove expired entry
                _cache.TryRemove(key, out _);
                return null;
            }

            return entry.Value as T;
        }

        return null;
    }

    public void Set<T>(string key, T value) where T : class
    {
        Set(key, value, _defaultTtl);
    }

    public void Set<T>(string key, T value, TimeSpan ttl) where T : class
    {
        if (string.IsNullOrEmpty(key) || value == null)
        {
            return;
        }

        var entry = new CacheEntry(value, ttl);
        _cache.AddOrUpdate(key, entry, (_, _) => entry);
    }

    public void Remove(string key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _cache.TryRemove(key, out _);
        }
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public bool Contains(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired)
            {
                _cache.TryRemove(key, out _);
                return false;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Background task that periodically removes expired entries
    /// </summary>
    private async Task CleanupExpiredEntriesAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1));

                var expiredKeys = _cache
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _cache.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    Debug.WriteLine($"[CACHE] Cleaned up {expiredKeys.Count} expired entries");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CACHE] Cleanup error: {ex.Message}");
            }
        }
    }
}
