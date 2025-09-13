// Services/Windows/WindowInfoCache.cs
using System;
using System.Collections.Concurrent;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;

namespace AutomationCore.Services.Windows
{
    /// <summary>
    /// Кэш информации о окнах с TTL
    /// </summary>
    public sealed class WindowInfoCache : IWindowInfoCache
    {
        private readonly ConcurrentDictionary<WindowHandle, CacheEntry> _cache = new();
        private readonly System.Threading.Timer _cleanupTimer;

        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromSeconds(5);

        public WindowInfoCache()
        {
            // Очистка кэша каждые 30 секунд
            _cleanupTimer = new System.Threading.Timer(CleanupExpired, null,
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public WindowInfo? GetCached(WindowHandle handle)
        {
            if (_cache.TryGetValue(handle, out var entry) && !entry.IsExpired)
            {
                return entry.WindowInfo;
            }

            // Удаляем просроченную запись
            _cache.TryRemove(handle, out _);
            return null;
        }

        public void SetCached(WindowHandle handle, WindowInfo info, TimeSpan? ttl = null)
        {
            var expiry = DateTime.UtcNow.Add(ttl ?? DefaultTtl);
            var entry = new CacheEntry(info, expiry);

            _cache.AddOrUpdate(handle, entry, (_, _) => entry);
        }

        public bool RemoveFromCache(WindowHandle handle)
        {
            return _cache.TryRemove(handle, out _);
        }

        public void ClearCache()
        {
            _cache.Clear();
        }

        private void CleanupExpired(object? state)
        {
            var keysToRemove = new List<WindowHandle>();

            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _cache.Clear();
        }

        private sealed record CacheEntry(WindowInfo WindowInfo, DateTime ExpiresAt)
        {
            public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        }
    }
}