// Template Cache - отвечает только за кэширование шаблонов
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AutomationCore.Core.Abstractions;
using AbstractionsTemplateData = AutomationCore.Core.Abstractions.TemplateData;

namespace AutomationCore.Infrastructure.Storage.Components
{
    /// <summary>
    /// Потокобезопасный кэш шаблонов с TTL и автоматической инвалидацией
    /// </summary>
    internal sealed class TemplateCache : IDisposable
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly System.Threading.Timer _cleanupTimer;
        private readonly TimeSpan _defaultTtl;
        private bool _disposed;

        public TemplateCache(TimeSpan? defaultTtl = null)
        {
            _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(30);

            // Запускаем таймер очистки каждые 5 минут
            _cleanupTimer = new System.Threading.Timer(CleanupExpiredEntries, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Получает шаблон из кэша
        /// </summary>
        public AbstractionsTemplateData? Get(string key)
        {
            if (_disposed)
                return null;

            if (_cache.TryGetValue(key, out var entry))
            {
                // Проверяем не истек ли TTL
                if (entry.ExpiresAt > DateTime.UtcNow)
                {
                    entry.LastAccessed = DateTime.UtcNow;
                    return entry.Data;
                }
                else
                {
                    // Удаляем истекший элемент
                    _cache.TryRemove(key, out _);
                }
            }

            return null;
        }

        /// <summary>
        /// Сохраняет шаблон в кэш
        /// </summary>
        public void Set(string key, AbstractionsTemplateData data, TimeSpan? ttl = null)
        {
            if (_disposed)
                return;

            var actualTtl = ttl ?? _defaultTtl;
            var entry = new CacheEntry
            {
                Data = data,
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow + actualTtl
            };

            _cache.AddOrUpdate(key, entry, (k, oldEntry) => entry);
        }

        /// <summary>
        /// Проверяет есть ли шаблон в кэше
        /// </summary>
        public bool Contains(string key)
        {
            return Get(key) != null;
        }

        /// <summary>
        /// Удаляет шаблон из кэша
        /// </summary>
        public bool Remove(string key)
        {
            return _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// Инвалидирует шаблон по ключу (например, при изменении файла)
        /// </summary>
        public void Invalidate(string key)
        {
            Remove(key);
        }

        /// <summary>
        /// Очищает весь кэш
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Получает статистику кэша
        /// </summary>
        public CacheStats GetStats()
        {
            var now = DateTime.UtcNow;
            int totalCount = 0;
            int expiredCount = 0;
            long totalMemoryBytes = 0;

            foreach (var kvp in _cache)
            {
                totalCount++;
                if (kvp.Value.ExpiresAt <= now)
                    expiredCount++;

                totalMemoryBytes += kvp.Value.Data?.Data.Length ?? 0;
            }

            return new CacheStats
            {
                TotalEntries = totalCount,
                ExpiredEntries = expiredCount,
                ActiveEntries = totalCount - expiredCount,
                EstimatedMemoryUsage = totalMemoryBytes
            };
        }

        private void CleanupExpiredEntries(object? state)
        {
            if (_disposed)
                return;

            var now = DateTime.UtcNow;
            var expiredKeys = new List<string>();

            foreach (var kvp in _cache)
            {
                if (kvp.Value.ExpiresAt <= now)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            // Логируем статистику если есть что почистить
            if (expiredKeys.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"TemplateCache: Cleaned up {expiredKeys.Count} expired entries");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _cleanupTimer?.Dispose();
            _cache.Clear();
            _disposed = true;
        }

        private sealed class CacheEntry
        {
            public required AbstractionsTemplateData Data { get; init; }
            public required DateTime CreatedAt { get; init; }
            public required DateTime ExpiresAt { get; init; }
            public DateTime LastAccessed { get; set; }
        }
    }

    /// <summary>
    /// Статистика кэша шаблонов
    /// </summary>
    public sealed record CacheStats
    {
        public int TotalEntries { get; init; }
        public int ActiveEntries { get; init; }
        public int ExpiredEntries { get; init; }
        public long EstimatedMemoryUsage { get; init; }
    }
}