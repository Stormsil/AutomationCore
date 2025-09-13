// AutomationCore/Core/Matching/Implementations.cs
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using OpenCvSharp;
using static AutomationCore.Core.EnhancedScreenCapture;

namespace AutomationCore.Core.Matching
{
    // ===== Препроцессинг =====
    public sealed class BasicPreprocessor : IPreprocessor
    {
        public Task<Mat> ProcessAsync(Mat src, AutomationCore.Core.Abstractions.PreprocessingOptions o)
        {
            if (src is null || src.Empty())
                return Task.FromResult(src?.Clone() ?? new Mat());

            Mat cur = src.Clone();

            if (o?.UseGray == true)
            {
                var tmp = new Mat();
                Cv2.CvtColor(cur, tmp, ColorConversionCodes.BGR2GRAY);
                cur.Dispose();
                cur = tmp;
            }

            if (o?.Blur is OpenCvSharp.Size k && (k.Width > 1 || k.Height > 1))
            {
                var tmp = new Mat();
                Cv2.GaussianBlur(cur, tmp, k, 0);
                cur.Dispose();
                cur = tmp;
            }

            if (o?.UseCanny == true)
            {
                var tmp = new Mat();
                Cv2.Canny(cur, tmp, 80, 160);
                cur.Dispose();
                cur = tmp;
            }

            return Task.FromResult(cur);
        }
    }

    // ===== Движок сопоставления =====
    public sealed class BasicMatchingEngine : IMatchingEngine
    {
        public Task<MatchResult> FindBestMatchAsync(Mat sourceP, Mat templP, AutomationCore.Core.Abstractions.MatchOptions o)
        {
            if (sourceP.Empty() || templP.Empty())
                return Task.FromResult<MatchResult>(null);

            // простая мульти-скейл стратегия
            double bestScore = double.NegativeInfinity;
            OpenCvSharp.Point bestLoc = default;
            double bestScale = 1.0;

            var (minS, maxS, step) = o.ScaleRange;

            for (double s = o.UseMultiScale ? minS : 1.0; s <= (o.UseMultiScale ? maxS : 1.0); s += (o.UseMultiScale ? step : 1.0))
            {
                using var templS = Math.Abs(s - 1.0) < 1e-9
                    ? templP.Clone()
                    : templP.Resize(new OpenCvSharp.Size(Math.Max(1, (int)(templP.Width * s)), Math.Max(1, (int)(templP.Height * s))));

                if (templS.Width > sourceP.Width || templS.Height > sourceP.Height)
                    continue;

                using var result = new Mat();
                Cv2.MatchTemplate(sourceP, templS, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);

                if (maxVal > bestScore)
                {
                    bestScore = maxVal;
                    bestLoc = maxLoc;
                    bestScale = s;
                }
            }

            if (double.IsNegativeInfinity(bestScore))
                return Task.FromResult<MatchResult>(null);

            int w = (int)Math.Round(templP.Width * bestScale);
            int h = (int)Math.Round(templP.Height * bestScale);

            var rect = new Rect(bestLoc.X, bestLoc.Y, w, h);
            var center = new System.Drawing.Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

            return Task.FromResult(new MatchResult(rect, center, bestScore, bestScale, bestScore >= o.Threshold));
        }
    }

    // ===== Кэш =====
    public sealed class MemoryMatchCache : IMatchCache
    {
        private readonly CacheOptions _opt;
        private readonly ConcurrentDictionary<string, (MatchResult value, DateTime exp)> _map = new();

        public MemoryMatchCache(CacheOptions opt) => _opt = opt ?? new CacheOptions();

        public bool TryGet(string key, out MatchResult result)
        {
            result = null;
            if (_map.TryGetValue(key, out var entry) && entry.exp > DateTime.UtcNow)
            {
                result = entry.value;
                return true;
            }
            return false;
        }

        public void Set(string key, MatchResult result, TimeSpan ttl)
        {
            _map[key] = (result, DateTime.UtcNow.Add(ttl <= TimeSpan.Zero ? _opt.DefaultTtl : ttl));
        }
    }
}
