// ====================================
// Enhanced ScreenCapture (refactored)
// Высокоуровневая обертка для WGC + OpenCV с кэшем препроцессинга,
// локальным ROI-поиском и минимизацией копирований памяти.
// ====================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Infrastructure.Storage;
using AutomationCore.Infrastructure.Capture;
using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;
using CvRect = OpenCvSharp.Rect;
using CvSize = OpenCvSharp.Size;
using SDPoint = System.Drawing.Point;



namespace AutomationCore.Core
{
    /// <summary>
    /// Высокоуровневый класс для захвата экрана с расширенными возможностями
    /// </summary>
    public class EnhancedScreenCapture : IDisposable
    {
        // ===== Public result type (единый) =====
        public sealed record MatchResult(System.Drawing.Rectangle Bounds,
                                          SDPoint Center,
                                          double Score,
                                          double Scale,
                                          bool IsHardPass);

        private readonly Dictionary<IntPtr, EnhancedWindowsGraphicsCapture> _captureInstances = new();
        private readonly object _lock = new();

        private readonly ITemplateStore _templates;
        private CaptureSettings _defaultSettings;
        private readonly TemplateMatchOptions _defaultMatchOptions;

        // Кэш препроцессинга шаблонов (после Gray/Blur/Canny, до масштабирования)
        private readonly TemplatePreprocessCache _prepCache = new();

        // Последние удачные координаты по ключу (для локального ROI)
        private readonly ConcurrentDictionary<string, SDPoint> _lastHits = new();

        public EnhancedScreenCapture(
            CaptureSettings defaultSettings = null,
            ITemplateStore templateStore = null,
            TemplateMatchOptions defaultMatchOptions = null)
        {
            _defaultSettings = defaultSettings ?? CaptureSettings.Default;
            _templates = templateStore ?? new TemplateStorageAdapter(new FileTemplateStorage("templates"));
            _defaultMatchOptions = defaultMatchOptions ?? TemplatePresets.Universal;
        }

        #region ---------- Window Management & Info ----------

        /// <summary>Находит все окна по части заголовка</summary>
        public WindowInfo[] FindWindows(string titlePattern, bool exactMatch = false)
        {
            var windows = new List<WindowInfo>();
            var pattern = exactMatch ? titlePattern : Normalize(titlePattern);

            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd)) return true;

                var info = GetWindowInfo(hwnd);
                if (info == null) return true;

                var normalizedTitle = Normalize(info.Title);

                bool matches = exactMatch
                    ? normalizedTitle.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                    : normalizedTitle.Contains(pattern);

                if (matches) windows.Add(info);
                return true;
            }, IntPtr.Zero);

            return windows.ToArray();
        }

        public Rect ToScreenRoi(IntPtr hwnd, Rect relativeRoi)
        {
            var wi = GetWindowInfo(hwnd) ?? throw new InvalidOperationException("Window not found");
            return new Rect(
                wi.Bounds.X + relativeRoi.X,
                wi.Bounds.Y + relativeRoi.Y,
                relativeRoi.Width,
                relativeRoi.Height
            );
        }

        /// <summary>Получает информацию об окне</summary>
        public WindowInfo GetWindowInfo(IntPtr hwnd)
        {
            if (!IsWindow(hwnd)) return null;

            var info = new WindowInfo { Handle = hwnd };

            // Title
            int len = GetWindowTextLength(hwnd);
            if (len > 0)
            {
                var sb = new StringBuilder(len + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                info.Title = sb.ToString();
            }

            // Class name
            var className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);
            info.ClassName = className.ToString();

            // Bounds
            GetWindowRect(hwnd, out var rect);
            info.Bounds = rect;

            // State
            var placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hwnd, ref placement);
            info.IsMinimized = placement.showCmd == SW_MINIMIZE;
            info.IsMaximized = placement.showCmd == SW_MAXIMIZE;

            // Process
            GetWindowThreadProcessId(hwnd, out var pid);
            info.ProcessId = (int)pid;

            try
            {
                var process = System.Diagnostics.Process.GetProcessById(info.ProcessId);
                info.ProcessName = process.ProcessName;
            }
            catch { /* ignore */ }

            return info;
        }

        /// <summary>Получает список всех видимых окон</summary>
        public WindowInfo[] GetAllWindows()
        {
            var windows = new List<WindowInfo>();

            EnumWindows((hwnd, _) =>
            {
                if (IsWindowVisible(hwnd))
                {
                    var info = GetWindowInfo(hwnd);
                    if (info != null && !string.IsNullOrWhiteSpace(info.Title))
                        windows.Add(info);
                }
                return true;
            }, IntPtr.Zero);

            return windows.ToArray();
        }

        #endregion

        #region ---------- Single Frame Capture ----------

        /// <summary>Захватывает окно по заголовку</summary>
        public async Task<Bitmap> CaptureWindowAsync(string windowTitle)
        {
            var windows = FindWindows(windowTitle);
            if (windows.Length == 0)
                throw new InvalidOperationException($"Window with title containing \"{windowTitle}\" not found");

            return await CaptureWindowAsync(windows[0].Handle);
        }

        /// <summary>Захватывает окно по handle</summary>
        public async Task<Bitmap> CaptureWindowAsync(IntPtr hwnd)
        {
            using var capture = new EnhancedWindowsGraphicsCapture(_defaultSettings);
            await capture.InitializeAsync(hwnd);
            return await capture.CaptureBitmapAsync();
        }

        /// <summary>Захватывает весь экран</summary>
        public async Task<Bitmap> CaptureScreenAsync(int monitorIndex = 0)
        {
            using var capture = new EnhancedWindowsGraphicsCapture(_defaultSettings);
            await capture.InitializeAsync(IntPtr.Zero, monitorIndex);
            return await capture.CaptureBitmapAsync();
        }

        /// <summary>Захватывает регион экрана</summary>
        public async Task<Bitmap> CaptureRegionAsync(Rectangle region, int monitorIndex = 0)
        {
            var settings = _defaultSettings.Clone();
            settings.RegionOfInterest = region;

            using var capture = new EnhancedWindowsGraphicsCapture(settings);
            await capture.InitializeAsync(IntPtr.Zero, monitorIndex);
            return await capture.CaptureBitmapAsync();
        }

        /// <summary>Захватывает несколько окон одновременно</summary>
        public async Task<Dictionary<IntPtr, Bitmap>> CaptureMultipleWindowsAsync(params IntPtr[] handles)
        {
            var tasks = handles.Select(async hwnd =>
            {
                var bitmap = await CaptureWindowAsync(hwnd);
                return new { Handle = hwnd, Bitmap = bitmap };
            });

            var results = await Task.WhenAll(tasks);
            return results.ToDictionary(r => r.Handle, r => r.Bitmap);
        }

        #endregion

        #region ---------- Stream Capture ----------

        /// <summary>Начинает потоковый захват окна</summary>
        public async Task<CaptureSession> StartCaptureSessionAsync(IntPtr hwnd, CaptureSettings settings = null)
        {
            var capture = new EnhancedWindowsGraphicsCapture(settings ?? _defaultSettings);
            await capture.InitializeAsync(hwnd);

            lock (_lock) { _captureInstances[hwnd] = capture; }

            var session = new CaptureSession(capture, hwnd);
            capture.StartCapture();
            return session;
        }

        /// <summary>Начинает захват экрана</summary>
        public async Task<CaptureSession> StartScreenCaptureAsync(int monitorIndex = 0, CaptureSettings settings = null)
        {
            var capture = new EnhancedWindowsGraphicsCapture(settings ?? _defaultSettings);
            await capture.InitializeAsync(IntPtr.Zero, monitorIndex);

            var handle = IntPtr.Zero; // ключ для всего экрана
            lock (_lock) { _captureInstances[handle] = capture; }

            var session = new CaptureSession(capture, handle);
            capture.StartCapture();
            return session;
        }

        /// <summary>Останавливает захват по hwnd</summary>
        public void StopCapture(IntPtr hwnd)
        {
            lock (_lock)
            {
                if (_captureInstances.TryGetValue(hwnd, out var capture))
                {
                    capture.StopCapture();
                    capture.Dispose();
                    _captureInstances.Remove(hwnd);
                }
            }
        }

        /// <summary>Останавливает все активные захваты</summary>
        public void StopAllCaptures()
        {
            lock (_lock)
            {
                foreach (var capture in _captureInstances.Values)
                {
                    capture.StopCapture();
                    capture.Dispose();
                }
                _captureInstances.Clear();
            }
        }

        #endregion

        #region ---------- OpenCV Integration ----------

        /// <summary>
        /// Захватывает кадр как OpenCV Mat (BGR 8UC3) с минимальными копиями.
        /// </summary>
        public async Task<Mat> CaptureMatAsync(IntPtr hwnd)
        {
            using var capture = new EnhancedWindowsGraphicsCapture(_defaultSettings);
            await capture.InitializeAsync(hwnd);
            var frame = await capture.CaptureFrameAsync();
            return ConvertToMat(frame);
        }

        /// <summary>
        /// Конвертирует CaptureFrame (BGRA byte[]) в OpenCV Mat (BGR 8UC3).
        /// Используем Mat.FromPixelData вместо устаревшего конструктора.
        /// </summary>
        public static Mat ConvertToMat(CaptureFrame frame)
        {
            if (frame == null || frame.Data == null)
                throw new ArgumentNullException(nameof(frame));

            var bgr = new Mat();
            GCHandle handle = default;
            try
            {
                handle = GCHandle.Alloc(frame.Data, GCHandleType.Pinned);
                var ptr = handle.AddrOfPinnedObject();

                using var bgraView = Mat.FromPixelData(frame.Height, frame.Width, MatType.CV_8UC4, ptr, frame.Stride);
                Cv2.CvtColor(bgraView, bgr, ColorConversionCodes.BGRA2BGR);
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }

            return bgr;
        }

        #endregion

        #region ---------- Template Matching (best / wait / all) ----------

        /// <summary>
        /// Базовый матчинг: ищет лучший матч на матрице экрана по предобработанному шаблону.
        /// </summary>
        private MatchResult? FindBestOnMat(Mat screenBgr, Mat templBgr, TemplateMatchOptions o)
        {
            if (screenBgr.Empty() || templBgr.Empty()) return null;

            // ROI на экране (абсолютные координаты)
            Mat view = screenBgr;
            var roiOffset = new SDPoint(0, 0);
            if (o.Roi is CvRect r && r.Width > 0 && r.Height > 0)
            {
                int x = Math.Clamp(r.X, 0, screenBgr.Cols - 1);
                int y = Math.Clamp(r.Y, 0, screenBgr.Rows - 1);
                int w = Math.Clamp(r.Width, 1, screenBgr.Cols - x);
                int h = Math.Clamp(r.Height, 1, screenBgr.Rows - y);
                var clamp = new CvRect(x, y, w, h);
                view = new Mat(screenBgr, clamp);
                roiOffset = new SDPoint(clamp.X, clamp.Y);
            }

            // Препроцессинг экрана/шаблона (gray/blur/canny)
            using var viewP = Prep(view, o);
            using var templP = _prepCache.GetOrCreate(templBgr, o);

            if (templP.Cols > viewP.Cols || templP.Rows > viewP.Rows) return null;

            // Поиск оптимального масштаба
            double bestScore = o.HigherIsBetter ? double.NegativeInfinity : double.PositiveInfinity;
            CvPoint bestLoc = default;
            double bestScale = 1.0;

            double start = o.ScaleMin, end = o.ScaleMax, step = o.ScaleStep <= 0 ? 0.01 : o.ScaleStep;
            if (start > end) (start, end) = (end, start);

            for (double s = start; s <= end + 1e-9; s += step)
            {
                if (Math.Abs(s - 1.0) < 1e-9)
                {
                    using var result = new Mat();
                    Cv2.MatchTemplate(viewP, templP, result, o.Algorithm, mask: o.Mask);
                    Cv2.MinMaxLoc(result, out var minVal, out var maxVal, out var minLoc, out var maxLoc);
                    double score = o.HigherIsBetter ? maxVal : minVal;
                    bool better = o.HigherIsBetter ? score > bestScore : score < bestScore;
                    if (better)
                    {
                        bestScore = score;
                        bestLoc = o.HigherIsBetter ? maxLoc : minLoc;
                        bestScale = 1.0;
                    }
                }
                else
                {
                    using var scaled = templP.Resize(new OpenCvSharp.Size(
                        Math.Max(1, (int)(templP.Cols * s)),
                        Math.Max(1, (int)(templP.Rows * s))),
                        0, 0, InterpolationFlags.Linear);

                    if (scaled.Cols > viewP.Cols || scaled.Rows > viewP.Rows) continue;

                    using var result = new Mat();
                    Cv2.MatchTemplate(viewP, scaled, result, o.Algorithm, mask: o.Mask);
                    Cv2.MinMaxLoc(result, out var minVal, out var maxVal, out var minLoc, out var maxLoc);

                    double score = o.HigherIsBetter ? maxVal : minVal;
                    bool better = o.HigherIsBetter ? score > bestScore : score < bestScore;
                    if (better)
                    {
                        bestScore = score;
                        bestLoc = o.HigherIsBetter ? maxLoc : minLoc;
                        bestScale = s;
                    }
                }
            }

            if (double.IsInfinity(bestScore)) return null;

            bool isHardPass = o.HigherIsBetter ? (bestScore >= o.Threshold)
                                               : (bestScore <= (1.0 - o.Threshold));

            int wT = (int)Math.Round(templBgr.Width * bestScale);
            int hT = (int)Math.Round(templBgr.Height * bestScale);
            int x0 = roiOffset.X + bestLoc.X;
            int y0 = roiOffset.Y + bestLoc.Y;

            var rect = new System.Drawing.Rectangle(x0, y0, wT, hT);
            var center = new SDPoint(x0 + wT / 2, y0 + hT / 2);

            return new MatchResult(rect, center, bestScore, bestScale, isHardPass);
        }

        /// <summary>
        /// Ищет лучший матч по ключу (одиночный снимок), с опциональным «жёстким» порогом.
        /// </summary>
        public async Task<MatchResult?> FindBestMatchByKeyAsync(
            string key, TemplateMatchOptions? opt = null, bool allowNear = true, CancellationToken ct = default)
        {
            var o = opt ?? _defaultMatchOptions;

            using var screenBgr = await CaptureMatAsync(IntPtr.Zero);
            using var templBgr = _templates.GetTemplate(key);

            // Если есть последняя удачная точка — сначала локальный ROI
            var local = LocalizeOptionsByLastHit(o, key, templBgr);
            var res = FindBestOnMat(screenBgr, templBgr, local);
            if (res is { } r1)
            {
                if (allowNear || r1.IsHardPass)
                {
                    _lastHits[key] = r1.Center;
                    return r1;
                }
            }

            // fallback: глобальный поиск, если локальный не подтвердился
            var global = o with { Roi = null };
            var res2 = FindBestOnMat(screenBgr, templBgr, global);
            if (res2 is { } r2 && (allowNear || r2.IsHardPass)) _lastHits[key] = r2.Center;
            return res2;
        }

        /// <summary>
        /// Ждёт появления шаблона, проверяя экран в цикле. Сначала локальный ROI вокруг последней точки,
        /// периодически — глобальный поиск (каждые globalRefreshTicks итераций).
        /// </summary>
        public async Task<MatchResult?> WaitForBestMatchByKeyAsync(
            string key,
            int timeoutMs = 10000,
            int checkIntervalMs = 120,
            TemplateMatchOptions? opt = null,
            bool allowNear = true,
            int globalRefreshTicks = 8,
            CancellationToken ct = default)
        {
            var o = opt ?? _defaultMatchOptions;
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            var session = await StartScreenCaptureAsync();
            using var templ = _templates.GetTemplate(key);

            try
            {
                int hits = 0;
                MatchResult? last = null;
                int tick = 0;

                while (DateTime.UtcNow < end)
                {
                    ct.ThrowIfCancellationRequested();

                    var frame = await session.GetNextFrameAsync(ct);
                    using var screen = ConvertToMat(frame);

                    TemplateMatchOptions currentOpt;

                    // локальный ROI несколько итераций, потом глобально «освежаемся»
                    if (_lastHits.TryGetValue(key, out var lastPt) && (tick % globalRefreshTicks != 0))
                        currentOpt = LocalizeOptionsByPoint(o, lastPt, templ);
                    else
                        currentOpt = o with { Roi = null };

                    var res = FindBestOnMat(screen, templ, currentOpt);
                    bool pass = res is not null && (allowNear || res.IsHardPass);

                    if (pass)
                    {
                        if (last is not null &&
                            Math.Abs(last.Center.X - res!.Center.X) < 2 &&
                            Math.Abs(last.Center.Y - res!.Center.Y) < 2)
                            hits++;
                        else
                            hits = 1;

                        last = res;
                        _lastHits[key] = res.Center;

                        if (hits >= Math.Max(1, o.ConsecutiveHits))
                            return res;
                    }
                    else
                    {
                        hits = 0;
                        last = null;
                    }

                    tick++;
                    await Task.Delay(checkIntervalMs, ct);
                }

                return null;
            }
            finally
            {
                session.Stop();
            }
        }

        /// <summary>
        /// Находит несколько совпадений (NMS). Масштабы — без перебора (s=1),
        /// ориентировано на повторяющиеся однотипные элементы.
        /// </summary>
        public async Task<IReadOnlyList<MatchResult>> FindAllMatchesByKeyAsync(
            string key, int maxResults = 5, double nmsOverlap = 0.3, TemplateMatchOptions? opt = null)
        {
            var o = opt ?? _defaultMatchOptions;
            using var screenBgr = await CaptureMatAsync(IntPtr.Zero);
            using var templBgr = _templates.GetTemplate(key);

            return FindAllMatches_Internal(screenBgr, templBgr, o, maxResults, nmsOverlap);
        }

        private IReadOnlyList<MatchResult> FindAllMatches_Internal(
            Mat screenBgr, Mat templBgr, TemplateMatchOptions o, int maxResults, double nmsOverlap)
        {
            var results = new List<MatchResult>();

            // ROI + препроцессинг
            Mat view = screenBgr;
            var roiOffset = new SDPoint(0, 0);
            if (o.Roi is CvRect r && r.Width > 0 && r.Height > 0)
            {
                int x = Math.Clamp(r.X, 0, screenBgr.Cols - 1);
                int y = Math.Clamp(r.Y, 0, screenBgr.Rows - 1);
                int roiW = Math.Clamp(r.Width, 1, screenBgr.Cols - x);
                int roiH = Math.Clamp(r.Height, 1, screenBgr.Rows - y);
                var clamp = new CvRect(x, y, roiW, roiH);
                view = new Mat(screenBgr, clamp);
                roiOffset = new SDPoint(clamp.X, clamp.Y);
            }

            using var viewP = Prep(view, o);
            using var templP = _prepCache.GetOrCreate(templBgr, o);

            using var result = new Mat();
            Cv2.MatchTemplate(viewP, templP, result, o.Algorithm, mask: o.Mask);

            using var work = result.Clone();
            int tW = templP.Width, tH = templP.Height;

            for (int i = 0; i < maxResults; i++)
            {
                Cv2.MinMaxLoc(work, out var minVal, out var maxVal, out var minLoc, out var maxLoc);
                double score = o.HigherIsBetter ? maxVal : minVal;
                var loc = o.HigherIsBetter ? maxLoc : minLoc;

                bool pass = o.HigherIsBetter ? (score >= o.Threshold) : (score <= (1.0 - o.Threshold));
                if (!pass) break;

                var rect = new System.Drawing.Rectangle(roiOffset.X + loc.X, roiOffset.Y + loc.Y, tW, tH);
                var center = new SDPoint(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

                results.Add(new MatchResult(rect, center, score, 1.0, true));

                // Подавляем окрестность (простая NMS через зануление)
                int supW = (int)(tW * (1.0 + nmsOverlap));
                int supH = (int)(tH * (1.0 + nmsOverlap));

                var sx = Math.Max(0, loc.X - (supW - tW) / 2);
                var sy = Math.Max(0, loc.Y - (supH - tH) / 2);
                var sw = Math.Min(supW, work.Cols - sx);
                var sh = Math.Min(supH, work.Rows - sy);

                if (sw <= 0 || sh <= 0) break;

                var sup = new CvRect(sx, sy, sw, sh);
                work[sup].SetTo(o.HigherIsBetter ? 0 : 1);
            }

            return results;
        }

        // -------- Public back-compat overloads --------
        public Task<SDPoint?> FindImageOnScreenByKeyAsync(string key, double threshold) =>
            FindBestMatchByKeyAsync(key, _defaultMatchOptions with { Threshold = threshold })
            .ContinueWith(t => t.Result?.Center);

        public Task<SDPoint?> WaitForImageByKeyAsync(
            string key, int timeoutMs, double threshold, int checkIntervalMs = 150) =>
            WaitForBestMatchByKeyAsync(key, timeoutMs, checkIntervalMs,
                _defaultMatchOptions with { Threshold = threshold })
            .ContinueWith(t => t.Result?.Center);

        #endregion

        #region ---------- Recording ----------

        /// <summary>Записывает видео захвата</summary>
        public async Task<VideoRecorder> StartRecordingAsync(IntPtr hwnd, string outputPath,
            int fps = 30, VideoCodec codec = VideoCodec.H264)
        {
            var session = await StartCaptureSessionAsync(hwnd);
            var recorder = new VideoRecorder(session, outputPath, fps, codec);
            await recorder.StartAsync();
            return recorder;
        }

        #endregion

        #region ---------- Utility Methods ----------

        /// <summary>Сохраняет скриншот в файл</summary>
        public async Task SaveScreenshotAsync(string filePath, IntPtr hwnd = default,
            ImageFormat format = null)
        {
            format ??= GetFormatFromExtension(Path.GetExtension(filePath));

            using var bitmap = hwnd == IntPtr.Zero
                ? await CaptureScreenAsync()
                : await CaptureWindowAsync(hwnd);

            bitmap.Save(filePath, format);
        }

        /// <summary>Делает серию скриншотов</summary>
        public async Task<string[]> CaptureTimelapseAsync(IntPtr hwnd, string outputDir,
            int count, int intervalMs)
        {
            var files = new List<string>();

            for (int i = 0; i < count; i++)
            {
                var fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}_{i:D3}.png";
                var filePath = Path.Combine(outputDir, fileName);

                await SaveScreenshotAsync(filePath, hwnd);
                files.Add(filePath);

                if (i < count - 1)
                    await Task.Delay(intervalMs);
            }

            return files.ToArray();
        }

        /// <summary>Сравнивает два изображения</summary>
        public static double CompareImages(Bitmap img1, Bitmap img2)
        {
            if (img1.Size != img2.Size) return 0;

            using var m1 = BitmapToMat(img1);
            using var m2 = BitmapToMat(img2);

            using var gray1 = new Mat();
            using var gray2 = new Mat();
            Cv2.CvtColor(m1, gray1, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(m2, gray2, ColorConversionCodes.BGR2GRAY);

            return ComputeSsim(gray1, gray2);
        }

        private static double ComputeSsim(Mat img1, Mat img2)
        {
            const double C1 = 6.5025, C2 = 58.5225;

            using var I1 = new Mat();
            using var I2 = new Mat();
            img1.ConvertTo(I1, MatType.CV_32F);
            img2.ConvertTo(I2, MatType.CV_32F);

            // mu
            using var mu1 = new Mat();
            using var mu2 = new Mat();
            Cv2.GaussianBlur(I1, mu1, new OpenCvSharp.Size(11, 11), 1.5);
            Cv2.GaussianBlur(I2, mu2, new OpenCvSharp.Size(11, 11), 1.5);

            using var mu1mu1 = mu1.Mul(mu1);
            using var mu2mu2 = mu2.Mul(mu2);
            using var mu1mu2 = mu1.Mul(mu2);

            // sigma^2 and sigma12
            using var I1I1_blur = new Mat();
            using var I2I2_blur = new Mat();
            using var I1I2_blur = new Mat();
            Cv2.GaussianBlur(I1.Mul(I1), I1I1_blur, new OpenCvSharp.Size(11, 11), 1.5);
            Cv2.GaussianBlur(I2.Mul(I2), I2I2_blur, new OpenCvSharp.Size(11, 11), 1.5);
            Cv2.GaussianBlur(I1.Mul(I2), I1I2_blur, new OpenCvSharp.Size(11, 11), 1.5);

            using var sigma1_sq = new Mat();
            using var sigma2_sq = new Mat();
            using var sigma12 = new Mat();
            Cv2.Subtract(I1I1_blur, mu1mu1, sigma1_sq);
            Cv2.Subtract(I2I2_blur, mu2mu2, sigma2_sq);
            Cv2.Subtract(I1I2_blur, mu1mu2, sigma12);

            using var t1 = new Mat();
            Cv2.AddWeighted(mu1mu2, 2, Mat.Zeros(mu1mu2.Size(), mu1mu2.Type()), 0, C1, t1);

            using var t2 = new Mat();
            Cv2.AddWeighted(sigma12, 2, Mat.Zeros(sigma12.Size(), sigma12.Type()), 0, C2, t2);

            using var numerator = t1.Mul(t2);

            using var t3 = new Mat();
            Cv2.Add(mu1mu1, mu2mu2, t3);
            Cv2.Add(t3, new Scalar(C1), t3);

            using var t4 = new Mat();
            Cv2.Add(sigma1_sq, sigma2_sq, t4);
            Cv2.Add(t4, new Scalar(C2), t4);

            using var denominator = t3.Mul(t4);

            using var ssimMap = new Mat();
            Cv2.Divide(numerator, denominator, ssimMap);

            var mssim = Cv2.Mean(ssimMap);
            return mssim.Val0;
        }

        #endregion

        #region ---------- Helpers & Preprocessing ----------

        private static Mat Prep(Mat srcBgr, TemplateMatchOptions o)
        {
            // Внимание: возвращаем новый Mat (цепочка копий), исходник не трогаем.
            Mat cur = srcBgr;

            if (o.UseGray)
            {
                var tmp = new Mat();
                Cv2.CvtColor(cur, tmp, ColorConversionCodes.BGR2GRAY);
                cur = tmp;
            }

            if (o.Blur is CvSize k && (k.Width > 1 || k.Height > 1))
            {
                var tmp = new Mat();
                Cv2.GaussianBlur(cur, tmp, k, 0);
                cur = tmp;
            }

            if (o.UseCanny)
            {
                var tmp = new Mat();
                Cv2.Canny(cur, tmp, 80, 160);
                cur = tmp;
            }

            return cur;
        }

        private TemplateMatchOptions LocalizeOptionsByLastHit(TemplateMatchOptions o, string key, Mat templBgr)
        {
            if (!_lastHits.TryGetValue(key, out var pt)) return o;
            return LocalizeOptionsByPoint(o, pt, templBgr);
        }

        /// <summary>
        /// Формирует локальный ROI вокруг точки <paramref name="center"/> (в пикселях экрана).
        /// Ширина/высота ROI = scaleFactor * размер шаблона.
        /// </summary>
        private TemplateMatchOptions LocalizeOptionsByPoint(TemplateMatchOptions o, SDPoint center, Mat templBgr, double scaleFactor = 3.0)
        {
            int w = (int)(templBgr.Width * scaleFactor);
            int h = (int)(templBgr.Height * scaleFactor);

            var roi = new CvRect(
                Math.Max(0, center.X - w / 2),
                Math.Max(0, center.Y - h / 2),
                w, h);

            return o with { Roi = roi };
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            var normalized = s.Normalize(NormalizationForm.FormKC)
                .Replace('\u00A0', ' ')  // NBSP
                .Replace('\u2013', '-')  // En dash
                .Replace('\u2014', '-'); // Em dash

            return Regex.Replace(normalized, @"\s+", " ").Trim().ToLowerInvariant();
        }

        private static ImageFormat GetFormatFromExtension(string extension)
        {
            return extension?.ToLower() switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".png" => ImageFormat.Png,
                ".bmp" => ImageFormat.Bmp,
                ".gif" => ImageFormat.Gif,
                ".tiff" or ".tif" => ImageFormat.Tiff,
                _ => ImageFormat.Png
            };
        }

        private static Mat BitmapToMat(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                using var view = Mat.FromPixelData(bitmap.Height, bitmap.Width, MatType.CV_8UC3, bmpData.Scan0, bmpData.Stride);
                return view.Clone(); // чтобы данные жили после UnlockBits
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }
        }

        #endregion

        #region ---------- Dispose ----------

        public void Dispose()
        {
            StopAllCaptures();
        }

        #endregion

        #region ---------- P/Invoke ----------

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rectangle lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public SDPoint ptMinPosition;
            public SDPoint ptMaxPosition;
            public Rectangle rcNormalPosition;
        }

        private const int SW_MINIMIZE = 6;
        private const int SW_MAXIMIZE = 3;

        #endregion
    }

    #region ---------- Supporting Classes ----------

    /// <summary>Информация об окне</summary>
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; }
        public string ClassName { get; set; }
        public Rectangle Bounds { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public bool IsMinimized { get; set; }
        public bool IsMaximized { get; set; }

        public override string ToString() =>
            $"{Title} [{ProcessName}] ({Bounds.Width}x{Bounds.Height})";
    }

    /// <summary>Сессия захвата</summary>
    public class CaptureSession : ICaptureSession
    {
        private readonly EnhancedWindowsGraphicsCapture _capture;
        private readonly IntPtr _windowHandle;

        public IntPtr WindowHandle => _windowHandle;
        public bool IsActive => _capture != null;

        internal CaptureSession(EnhancedWindowsGraphicsCapture capture, IntPtr windowHandle)
        {
            _capture = capture;
            _windowHandle = windowHandle;
        }

        /// <summary>Подписывается на событие нового кадра</summary>
        public void OnFrameCaptured(EventHandler<FrameCapturedEventArgs> handler)
        {
            _capture.FrameCaptured += handler;
        }

        /// <summary>Получает следующий кадр</summary>
        public Task<CaptureFrame> GetNextFrameAsync(CancellationToken cancellationToken = default)
            => _capture.GetNextFrameAsync(cancellationToken);

        /// <summary>Получает последний кадр</summary>
        public CaptureFrame GetLastFrame() => _capture.GetLastFrame();

        /// <summary>Получает метрики</summary>
        public CaptureMetrics GetMetrics() => _capture.GetMetrics();

        /// <summary>Останавливает захват</summary>
        public void Stop() => _capture.StopCapture();

        public void Dispose()
        {
            Stop();
            _capture?.Dispose();
        }
    }

    /// <summary>Видео рекордер</summary>
    public class VideoRecorder : IDisposable
    {
        private readonly CaptureSession _session;
        private readonly string _outputPath;
        private readonly int _fps;
        private readonly VideoCodec _codec;
        private VideoWriter _writer;
        private CancellationTokenSource _recordingCts;
        private Task _recordingTask;

        public bool IsRecording { get; private set; }
        public TimeSpan Duration { get; private set; }
        public long FramesRecorded { get; private set; }

        internal VideoRecorder(CaptureSession session, string outputPath, int fps, VideoCodec codec)
        {
            _session = session;
            _outputPath = outputPath;
            _fps = fps;
            _codec = codec;
        }

        public async Task StartAsync()
        {
            if (IsRecording) return;

            var firstFrame = await _session.GetNextFrameAsync();

            var fourcc = _codec switch
            {
                VideoCodec.H264 => VideoWriter.FourCC('H', '2', '6', '4'),
                VideoCodec.MJPEG => VideoWriter.FourCC('M', 'J', 'P', 'G'),
                VideoCodec.XVID => VideoWriter.FourCC('X', 'V', 'I', 'D'),
                _ => VideoWriter.FourCC('M', 'P', '4', 'V')
            };

            _writer = new VideoWriter(_outputPath, fourcc, _fps,
                new OpenCvSharp.Size(firstFrame.Width, firstFrame.Height));

            if (!_writer.IsOpened())
                throw new InvalidOperationException("Failed to open video writer");

            IsRecording = true;
            _recordingCts = new CancellationTokenSource();

            _recordingTask = Task.Run(async () => await RecordingLoop(_recordingCts.Token));
        }

        private async Task RecordingLoop(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var frameInterval = TimeSpan.FromSeconds(1.0 / _fps);
            var nextFrameTime = startTime;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var frame = await _session.GetNextFrameAsync(cancellationToken);
                    using var mat = EnhancedScreenCapture.ConvertToMat(frame);

                    _writer.Write(mat);
                    FramesRecorded++;

                    Duration = DateTime.UtcNow - startTime;

                    // Maintain target FPS
                    nextFrameTime += frameInterval;
                    var delay = nextFrameTime - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (OperationCanceledException) { break; }
            }
        }

        public async Task StopAsync()
        {
            if (!IsRecording) return;

            _recordingCts?.Cancel();
            if (_recordingTask != null)
                await _recordingTask;

            _writer?.Release();
            _writer?.Dispose();

            IsRecording = false;
        }

        public void Dispose()
        {
            StopAsync().Wait();
            _writer?.Dispose();
            _recordingCts?.Dispose();
        }
    }

    public enum VideoCodec
    {
        H264,
        MJPEG,
        XVID,
        MP4V
    }

    #endregion

    #region ---------- Internal: Template Preprocess Cache ----------

    /// <summary>
    /// Кэширует предобработанные версии шаблонов (Gray/Blur/Canny).
    /// Масштабирование НЕ кэшируем (слишком много вариантов) — масштаб делаем на лету.
    /// </summary>
    internal sealed class TemplatePreprocessCache : IDisposable
    {
        private readonly ConcurrentDictionary<Key, Mat> _cache = new();

        public Mat GetOrCreate(Mat templBgr, TemplateMatchOptions o)
        {
            var k = new Key(o.UseGray, o.UseCanny, o.Blur);

            if (_cache.TryGetValue(k, out var cached) && !cached.Empty())
                return cached.Clone();

            // Строим новую версию
            Mat cur = templBgr;

            if (o.UseGray)
            {
                var tmp = new Mat();
                Cv2.CvtColor(cur, tmp, ColorConversionCodes.BGR2GRAY);
                cur = tmp;
            }

            if (o.Blur is CvSize b && (b.Width > 1 || b.Height > 1))
            {
                var tmp = new Mat();
                Cv2.GaussianBlur(cur, tmp, b, 0);
                cur = tmp;
            }

            if (o.UseCanny)
            {
                var tmp = new Mat();
                Cv2.Canny(cur, tmp, 80, 160);
                cur = tmp;
            }

            _cache[k] = cur.Clone();
            return cur;
        }

        public void Dispose()
        {
            foreach (var kv in _cache)
                kv.Value.Dispose();
            _cache.Clear();
        }

        internal readonly record struct Key(bool Gray, bool Canny, CvSize? Blur);
    }

    #endregion
}
