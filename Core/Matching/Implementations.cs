// AutomationCore/Core/Matching/Implementations.cs
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using OpenCvSharp;
using static AutomationCore.Core.EnhancedScreenCapture;
using System.Collections.Concurrent;


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

            var mode = o?.Mode ?? TemplateMatchModes.CCoeffNormed;
            var mask = o?.Mask;

            bool HigherIsBetter = mode != TemplateMatchModes.SqDiff &&
                                  mode != TemplateMatchModes.SqDiffNormed;

            double bestScore = HigherIsBetter ? double.NegativeInfinity : double.PositiveInfinity;
            OpenCvSharp.Point bestLoc = default;
            double bestScale = 1.0;

            var (minS, maxS, step0) = o?.ScaleRange ?? new AutomationCore.Core.Abstractions.ScaleRange(1.0, 1.0, 0.01);
            double step = (o?.UseMultiScale == true) ? Math.Max(0.005, step0) : 1.0;
            double sStart = (o?.UseMultiScale == true) ? Math.Min(minS, maxS) : 1.0;
            double sEnd = (o?.UseMultiScale == true) ? Math.Max(minS, maxS) : 1.0;

            for (double s = sStart; s <= sEnd + 1e-9; s += step)
            {
                using var templS = (Math.Abs(s - 1.0) < 1e-9)
                    ? templP.Clone()
                    : templP.Resize(new OpenCvSharp.Size(
                        Math.Max(1, (int)(templP.Width * s)),
                        Math.Max(1, (int)(templP.Height * s))));

                if (templS.Width > sourceP.Width || templS.Height > sourceP.Height)
                    continue;

                using var result = new Mat();
                // mask может быть null — OpenCV нормально это принимает (игнор)
                Cv2.MatchTemplate(sourceP, templS, result, mode, mask);

                Cv2.MinMaxLoc(result, out var minVal, out var maxVal, out var minLoc, out var maxLoc);

                double score = HigherIsBetter ? maxVal : minVal;
                bool better = HigherIsBetter ? (score > bestScore) : (score < bestScore);
                if (better)
                {
                    bestScore = score;
                    bestLoc = HigherIsBetter ? maxLoc : minLoc;
                    bestScale = s;
                }
            }

            if ((HigherIsBetter && double.IsNegativeInfinity(bestScore)) ||
                (!HigherIsBetter && double.IsPositiveInfinity(bestScore)))
                return Task.FromResult<MatchResult>(null);

            int w = (int)Math.Round(templP.Width * bestScale);
            int h = (int)Math.Round(templP.Height * bestScale);

            var rect = new System.Drawing.Rectangle(bestLoc.X, bestLoc.Y, w, h);
            var center = new System.Drawing.Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            bool pass = HigherIsBetter ? (bestScore >= (o?.Threshold ?? 0.9))
                                       : (bestScore <= (1.0 - (o?.Threshold ?? 0.9)));

            return Task.FromResult(new MatchResult(rect, center, bestScore, bestScale, pass));
        }

    }


    // ===== Кэш =====
    public sealed class MemoryMatchCache : IMatchCache
    {
        private readonly MatchCacheOptions _opt;
        private readonly ConcurrentDictionary<string, (MatchResult value, DateTime exp)> _map = new();

        public MemoryMatchCache(MatchCacheOptions opt) => _opt = opt ?? new MatchCacheOptions();

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
            var exp = DateTime.UtcNow.Add(ttl <= TimeSpan.Zero ? _opt.DefaultTtl : ttl);
            _map[key] = (result, exp);
        }
    }
}
