// Infrastructure/Matching/MemoryMatchCache.cs
using System;
using System.Collections.Concurrent;
using AutomationCore.Core.Domain.Matching;
using AutomationCore.Core.Models;

namespace AutomationCore.Infrastructure.Matching
{
    /// <summary>
    /// Кэш результатов поиска шаблонов в памяти
    /// </summary>
    public sealed class MemoryMatchCache : AutomationCore.Core.Abstractions.IMatchCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly MatchCacheOptions _options;

        public MemoryMatchCache(MatchCacheOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public bool TryGet(string key, out MatchResult result)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow - entry.CreatedAt < _options.DefaultTtl)
                {
                    result = entry.Result;
                    return true;
                }
                else
                {
                    // Удаляем устаревшую запись
                    _cache.TryRemove(key, out _);
                }
            }

            result = MatchResult.NotFound;
            return false;
        }

        public void Set(string key, MatchResult result, TimeSpan ttl)
        {
            var entry = new CacheEntry(result, DateTime.UtcNow);
            _cache.AddOrUpdate(key, entry, (k, old) => entry);

            // Простая очистка если кэш переполнен
            if (_cache.Count > _options.Capacity)
            {
                CleanupOldEntries();
            }
        }

        private void CleanupOldEntries()
        {
            var cutoff = DateTime.UtcNow.Subtract(_options.DefaultTtl);
            var keysToRemove = new List<string>();

            foreach (var kvp in _cache)
            {
                if (kvp.Value.CreatedAt < cutoff)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }

        // Реализация интерфейса IMatchCache из Core.Abstractions
        public ValueTask<MatchingResult?> GetAsync(string key, CancellationToken ct = default)
        {
            if (TryGet(key, out var result))
            {
                // Конвертируем MatchResult в MatchingResult
                var matchingResult = new MatchingResult
                {
                    Success = result.IsMatch,
                    BestMatch = result.IsMatch ? result : null,
                    AllMatches = result.IsMatch ? new[] { result } : Array.Empty<MatchResult>()
                };
                return ValueTask.FromResult<MatchingResult?>(matchingResult);
            }
            return ValueTask.FromResult<MatchingResult?>(null);
        }

        public ValueTask SetAsync(string key, MatchingResult result, TimeSpan? ttl = null, CancellationToken ct = default)
        {
            if (result.Success && result.BestMatch.HasValue)
            {
                Set(key, result.BestMatch.Value, ttl ?? _options.DefaultTtl);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> RemoveAsync(string key, CancellationToken ct = default)
        {
            bool removed = _cache.TryRemove(key, out _);
            return ValueTask.FromResult(removed);
        }

        public ValueTask ClearAsync(CancellationToken ct = default)
        {
            _cache.Clear();
            return ValueTask.CompletedTask;
        }

        private readonly record struct CacheEntry(MatchResult Result, DateTime CreatedAt);
    }
}