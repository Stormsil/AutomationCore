using System;
using System.Drawing;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Core.Domain.Matching;
using OpenCvSharp;
using TemplateMatchOptions = AutomationCore.Core.Models.TemplateMatchOptions;
using MatchOptions = AutomationCore.Core.Models.MatchOptions;
using PreprocessingOptions = AutomationCore.Core.Models.PreprocessingOptions;

namespace AutomationCore.Core.Matching
{
    /// <summary>
    /// Минимальная реализация TemplateMatcherService без внешних зависимостей.
    /// </summary>
    public class TemplateMatcherService : ITemplateMatcherService
    {
        private readonly ITemplateStore _templateStore;
        private readonly IPreprocessor _preproc;
        private readonly IMatchingEngine _engine;

        // DI-конструктор (предпочтительный)
        public TemplateMatcherService(ITemplateStore templateStore, IPreprocessor preproc, IMatchingEngine engine)
        {
            _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
            _preproc = preproc ?? throw new ArgumentNullException(nameof(preproc));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        // запасной конструктор для назад-совместимости (без DI)
        public TemplateMatcherService(ITemplateStore templateStore)
            : this(templateStore, new BasicPreprocessor(), new BasicMatchingEngine())
        { }

        public async Task<MatchResult> FindAsync(string templateKey, TemplateMatchOptions presets)
        {
            // Захватываем текущий экран в Mat и делегируем в общую реализацию
            // TODO: Нужно использовать IScreenCapture сервис
            throw new NotImplementedException("Screen capture не реализован - нужно использовать DI");

            var options = Map(presets);
            return await FindAsync(templateKey, screen, options);
        }

        public async Task<MatchResult> FindAsync(string templateKey, Mat sourceImage, MatchOptions options = null)
        {
            options ??= new MatchOptions();

            using var templBgr = _templateStore.GetTemplate(templateKey);
            if (templBgr.Empty() || sourceImage.Empty())
                return null;

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

            using var viewP = await _preproc.ProcessAsync(view, options.Preprocessing);
            using var templP = await _preproc.ProcessAsync(templBgr, options.Preprocessing);

            var hit = await _engine.FindBestMatchAsync(viewP, templP, options);
            if (hit is null) return null;

            // корректируем координаты в системе экрана
            var bounds = new System.Drawing.Rectangle(
                roiOffset.X + hit.Bounds.X,
                roiOffset.Y + hit.Bounds.Y,
                hit.Bounds.Width,
                hit.Bounds.Height);

            var center = new System.Drawing.Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);

            return new MatchResult(bounds, center, hit.Score, hit.Scale, hit.IsHardPass);
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
                ScaleRange = new ScaleRange(t?.ScaleMin ?? 1.0, t?.ScaleMax ?? 1.0, t?.ScaleStep ?? 0.01),
                Mode = t?.Mode ?? TemplateMatchModes.CCoeffNormed,
                Mask = t?.Mask,
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
