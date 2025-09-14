// Core/Models/CacheModels.cs
using System;
using System.Collections.Concurrent;
using OpenCvSharp;

namespace AutomationCore.Core.Models
{
    /// <summary>
    /// Простая реализация кэша препроцессинга шаблонов
    /// </summary>
    public sealed class TemplatePreprocessCache
    {
        private readonly ConcurrentDictionary<string, (Mat mat, DateTime expiry)> _cache = new();
        private readonly TimeSpan _ttl = TimeSpan.FromMinutes(10);

        public Mat? Get(string key)
        {
            if (_cache.TryGetValue(key, out var entry) && entry.expiry > DateTime.UtcNow)
            {
                return entry.mat?.Clone();
            }
            return null;
        }

        public void Set(string key, Mat mat)
        {
            var expiry = DateTime.UtcNow.Add(_ttl);
            _cache[key] = (mat?.Clone(), expiry);
        }

        public void Clear()
        {
            foreach (var entry in _cache.Values)
            {
                entry.mat?.Dispose();
            }
            _cache.Clear();
        }
    }
}