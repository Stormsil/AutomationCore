// TemplatePreprocessCache (extracted from monolith)
// Кэш для препроцессинга шаблонов изображений
using System;
using System.Collections.Concurrent;
using AutomationCore.Core.Models;
using OpenCvSharp;

namespace AutomationCore.Core.Domain.Matching
{
    /// <summary>
    /// Кэш препроцессинга шаблонов для ускорения повторных поисков
    /// </summary>
    internal sealed class TemplatePreprocessCache : IDisposable
    {
        private readonly ConcurrentDictionary<CacheKey, Mat> _cache = new();
        private bool _disposed;

        /// <summary>
        /// Получает из кэша или создает предобработанную версию шаблона
        /// </summary>
        public Mat GetOrCreate(Mat templateBgr, TemplateMatchOptions options)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TemplatePreprocessCache));

            var key = new CacheKey(options.UseGray, options.UseCanny, options.Blur?.Width ?? 0);

            if (_cache.TryGetValue(key, out var cached) && !cached.Empty())
                return cached.Clone();

            // Строим новую предобработанную версию
            Mat current = templateBgr;

            if (options.UseGray)
            {
                var grayMat = new Mat();
                Cv2.CvtColor(current, grayMat, ColorConversionCodes.BGR2GRAY);
                current = grayMat;
            }

            if (options.Blur?.Width > 0)
            {
                var blurSize = options.Blur.Value;
                if (blurSize.Width > 1 || blurSize.Height > 1)
                {
                    var blurredMat = new Mat();
                    Cv2.GaussianBlur(current, blurredMat, blurSize, 0);
                    current = blurredMat;
                }
            }

            if (options.UseCanny)
            {
                var cannyMat = new Mat();
                Cv2.Canny(current, cannyMat, 80, 160);
                current = cannyMat;
            }

            // Сохраняем в кэш копию
            var cachedVersion = current.Clone();
            _cache[key] = cachedVersion;

            return current;
        }

        /// <summary>
        /// Очищает кэш
        /// </summary>
        public void Clear()
        {
            if (_disposed) return;

            foreach (var kvp in _cache)
            {
                kvp.Value?.Dispose();
            }
            _cache.Clear();
        }

        /// <summary>
        /// Получает количество закэшированных элементов
        /// </summary>
        public int Count => _cache.Count;

        public void Dispose()
        {
            if (_disposed) return;

            Clear();
            _disposed = true;
        }

        /// <summary>
        /// Ключ кэша для комбинации параметров предобработки
        /// </summary>
        internal readonly record struct CacheKey(bool Gray, bool Canny, double Blur);
    }
}