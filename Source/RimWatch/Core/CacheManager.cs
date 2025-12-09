using System;
using System.Collections.Generic;
using Verse;

namespace RimWatch.Core
{
    /// <summary>
    /// Centralized caching system for expensive calculations.
    /// Reduces performance impact by caching analysis results.
    /// </summary>
    public static class CacheManager
    {
        // Cache entries with expiration times
        private static Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();

        // Default cache durations (in ticks)
        public const int CACHE_1_SECOND = 60;
        public const int CACHE_10_SECONDS = 600;
        public const int CACHE_30_SECONDS = 1800;
        public const int CACHE_1_MINUTE = 3600;
        public const int CACHE_5_MINUTES = 18000;

        /// <summary>
        /// Get cached value or compute and cache it.
        /// </summary>
        public static T GetOrCompute<T>(string key, Func<T> computeFunc, int cacheDurationTicks)
        {
            int currentTick = Find.TickManager.TicksGame;

            // Check if cached and not expired
            if (_cache.TryGetValue(key, out CacheEntry entry))
            {
                if (currentTick < entry.ExpirationTick)
                {
                    // Cache hit
                    return (T)entry.Value;
                }
                else
                {
                    // Expired - remove
                    _cache.Remove(key);
                }
            }

            // Cache miss or expired - compute new value
            T value = computeFunc();

            // Store in cache
            _cache[key] = new CacheEntry
            {
                Value = value,
                ExpirationTick = currentTick + cacheDurationTicks
            };

            return value;
        }

        /// <summary>
        /// Invalidate specific cache entry.
        /// </summary>
        public static void Invalidate(string key)
        {
            _cache.Remove(key);
        }

        /// <summary>
        /// Invalidate all cache entries matching pattern.
        /// </summary>
        public static void InvalidatePattern(string pattern)
        {
            var keysToRemove = new List<string>();

            foreach (var key in _cache.Keys)
            {
                if (key.Contains(pattern))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }

        /// <summary>
        /// Clear all cache.
        /// </summary>
        public static void ClearAll()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Clean up expired entries.
        /// Call periodically to prevent memory bloat.
        /// </summary>
        public static void CleanupExpired()
        {
            int currentTick = Find.TickManager.TicksGame;
            var keysToRemove = new List<string>();

            foreach (var kvp in _cache)
            {
                if (currentTick >= kvp.Value.ExpirationTick)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }

        /// <summary>
        /// Get cache statistics for debugging.
        /// </summary>
        public static CacheStats GetStats()
        {
            int currentTick = Find.TickManager.TicksGame;
            int validEntries = 0;
            int expiredEntries = 0;

            foreach (var entry in _cache.Values)
            {
                if (currentTick < entry.ExpirationTick)
                    validEntries++;
                else
                    expiredEntries++;
            }

            return new CacheStats
            {
                TotalEntries = _cache.Count,
                ValidEntries = validEntries,
                ExpiredEntries = expiredEntries
            };
        }
    }

    /// <summary>
    /// Cache entry with expiration.
    /// </summary>
    internal class CacheEntry
    {
        public object Value { get; set; }
        public int ExpirationTick { get; set; }
    }

    /// <summary>
    /// Cache statistics.
    /// </summary>
    public class CacheStats
    {
        public int TotalEntries { get; set; }
        public int ValidEntries { get; set; }
        public int ExpiredEntries { get; set; }
    }
}

