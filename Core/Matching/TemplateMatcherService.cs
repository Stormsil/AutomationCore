using System;
using System.Drawing;
using System.Threading.Tasks;
using AutomationCore.Assets;
using AutomationCore.Core.Abstractions;
using OpenCvSharp;
using static AutomationCore.Core.EnhancedScreenCapture;

namespace AutomationCore.Core.Matching
{
    /// <summary>
    /// Минимальная реализация TemplateMatcherService без внешних зависимостей.
    /// </summary>
    public class TemplateMatcherService : ITemplateMatcherService
    {
        private readonly ITemplateStore _templateStore;

        public TemplateMatcherService(ITemplateStore templateStore)
        {
            _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
        }

        public async Task<MatchResult> FindAsync(string templateKey, TemplateMatchOptions presets)
        {
            // Захватываем текущий экран в Mat и делегируем в общую реализацию
            using var esc = new EnhancedScreenCapture();
            using var screen = await esc.CaptureMatAsync(IntPtr.Zero);

            var options = Map(presets);
            return await FindAsync(templateKey, screen, options);
        }

        public Task<MatchResult> FindAsync(string templateKey, Mat sourceImage, MatchOptions options = null)
        {
            options ??= new MatchOptions();

            // Загружаем шаблон (BGR 8UC3)
            using var templBgr = _templateStore.GetTemplate(templateKey);

            // ROI на источнике
            Mat view = sourceImage;
            var roiOffset = new System.Drawing.Point(0, 0);
            if (options.SearchRegion is Rectangle r && r.Width > 0 && r.Height > 0)
            {
                int x = Math.Clamp(r.X, 0, sourceImage.Cols - 1);
                int y = Math.Clamp(r.Y, 0, sourceImage.Rows - 1);
                int w = Math.Clamp(r.Width, 1, sourceImage.Cols - x);
                int h = Math.Clamp(r.Height, 1, sourceImage.Rows - y);

                var clamp = new OpenCvSharp.Rect(x, y, w, h);
                view = new Mat(sourceImage, clamp);
                roiOffset = new System.Drawing.Point(clamp.X, clamp.Y);
            }

            using var viewP = Prep(view, options.Preprocessing);
            using var templP = Prep(templBgr, options.Preprocessing);

            if (templP.Empty() || viewP.Empty()) return Task.FromResult<MatchResult>(null);
            if (templP.Cols > viewP.Cols || templP.Rows > viewP.Rows) return Task.FromResult<MatchResult>(null);

            // Поиск лучшего совпадения (по умолчанию CCoeffNormed; чем больше, тем лучше)
            double bestScore = double.NegativeInfinity;
            OpenCvSharp.Point bestLoc = default;
            double bestScale = 1.0;

            double sMin = options.UseMultiScale ? Math.Min(options.ScaleRange.Min, options.ScaleRange.Max) : 1.0;
            double sMax = options.UseMultiScale ? Math.Max(options.ScaleRange.Min, options.ScaleRange.Max) : 1.0;
            double step = options.UseMultiScale ? Math.Max(0.005, (sMax - sMin) / 12.0) : 1.0; // адаптивный шаг

            for (double s = sMin; s <= sMax + 1e-9; s += step)
            {
                using var templS = (Math.Abs(s - 1.0) < 1e-9)
                    ? templP.Clone()
                    : templP.Resize(new OpenCvSharp.Size(
                        Math.Max(1, (int)(templP.Cols * s)),
                        Math.Max(1, (int)(templP.Rows * s))));

                if (templS.Cols > viewP.Cols || templS.Rows > viewP.Rows) continue;

                using var result = new Mat();
                Cv2.MatchTemplate(viewP, templS, result, TemplateMatchModes.CCoeffNormed);
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

            bool pass = bestScore >= options.Threshold;

            int wT = (int)Math.Round(templBgr.Width * bestScale);
            int hT = (int)Math.Round(templBgr.Height * bestScale);
            int x0 = roiOffset.X + bestLoc.X;
            int y0 = roiOffset.Y + bestLoc.Y;

            var bounds = new System.Windows.Rect(x0, y0, wT, hT);
            var center = new System.Drawing.Point(x0 + wT / 2, y0 + hT / 2);

            return Task.FromResult(new MatchResult(bounds, center, bestScore, bestScale, pass));
        }

        // ---- helpers ----

        private static Mat Prep(Mat srcBgr, PreprocessingOptions p)
        {
            Mat cur = srcBgr;

            if (p?.UseGray == true)
            {
                var tmp = new Mat();
                Cv2.CvtColor(cur, tmp, ColorConversionCodes.BGR2GRAY);
                cur = tmp;
            }

            if (p?.Blur is OpenCvSharp.Size k && (k.Width > 1 || k.Height > 1))
            {
                var tmp = new Mat();
                Cv2.GaussianBlur(cur, tmp, k, 0);
                cur = tmp;
            }

            if (p?.UseCanny == true)
            {
                var tmp = new Mat();
                Cv2.Canny(cur, tmp, 80, 160);
                cur = tmp;
            }

            return cur;
        }

        private static MatchOptions Map(TemplateMatchOptions t)
        {
            var opt = new MatchOptions
            {
                Threshold = t?.Threshold ?? 0.90,
                UseMultiScale = t is { ScaleMin: var a, ScaleMax: var b } && Math.Abs(a - b) > 1e-9,
                ScaleRange = new ScaleRange(t?.ScaleMin ?? 1.0, t?.ScaleMax ?? 1.0),
                Preprocessing = new PreprocessingOptions
                {
                    UseGray = t?.UseGray ?? true,
                    UseCanny = t?.UseCanny ?? false,
                    Blur = t?.Blur
                }
            };

            if (t?.Roi is OpenCvSharp.Rect r && r.Width > 0 && r.Height > 0)
                opt.SearchRegion = new Rectangle(r.X, r.Y, r.Width, r.Height);

            return opt;
        }
    }
}
