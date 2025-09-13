// AutomationCore/Core/Services/WindowImageSearch.cs
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using AutomationCore.Assets;
using AutomationCore.Capture;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Matching;
using OpenCvSharp;

using SDPoint = System.Drawing.Point;
using SDRect = System.Drawing.Rectangle;

namespace AutomationCore.Core.Services
{
    /// <summary>
    /// Поиск шаблона внутри конкретного окна (неактивное/перекрытое допустимо).
    /// Если окно свернуто — восстанавливает его без активации и опускает на дно.
    /// </summary>
    public sealed class WindowImageSearch : IDisposable
    {
        private readonly ITemplateStore _store;
        private readonly ITemplateMatcherService _matcher;
        private readonly CaptureSettings _captureSettings;

        /// <param name="store">
        /// Источник шаблонов. Если null — будет создан FileTemplateStore с папкой
        /// &lt;AppContext.BaseDirectory&gt;/assets/templates.
        /// </param>
        /// <param name="settings">Настройки захвата WGC.</param>
        public WindowImageSearch(ITemplateStore store = null, CaptureSettings settings = null)
        {
            _store = store ?? new FileTemplateStore(new TemplateStoreOptions
            {
                BasePath = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "templates"),
                WatchForChanges = true
            }, _: null);

            _matcher = new TemplateMatcherService(_store);
            _captureSettings = settings ?? CaptureSettings.Default;
        }

        /// <summary>
        /// Ищет шаблон <paramref name="templateKey"/> внутри окна <paramref name="hwnd"/>.
        /// Возвращает совпадение в координатах ЭКРАНА.
        /// </summary>
        public async Task<EnhancedScreenCapture.MatchResult?> FindAsync(
            IntPtr hwnd,
            string templateKey,
            TemplateMatchOptions presets = null,
            bool ensureWindowReady = true,
            CancellationToken ct = default)
        {
            if (hwnd == IntPtr.Zero) throw new ArgumentException("hwnd is null");
            if (string.IsNullOrWhiteSpace(templateKey)) throw new ArgumentNullException(nameof(templateKey));

            // подготовим окно (свернуто/странные координаты)
            if (ensureWindowReady)
                await EnsureWindowReadyAsync(hwnd, ct);

            // один снимок окна -> Mat
            using var wgc = new EnhancedWindowsGraphicsCapture(_captureSettings);
            await wgc.InitializeAsync(hwnd);
            using var frame = await wgc.CaptureFrameAsync();
            using var mat = EnhancedScreenCapture.ConvertToMat(frame);

            var options = MapToMatchOptions(presets ?? TemplatePresets.Universal);
            var hit = await _matcher.FindAsync(templateKey, mat, options);

            if (hit is null) return null;

            // Перевод из координат окна в координаты экрана
            if (!GetWindowRect(hwnd, out var wr))
                return null;

            var screenRect = new SDRect(
                wr.Left + hit.Bounds.X,
                wr.Top + hit.Bounds.Y,
                hit.Bounds.Width,
                hit.Bounds.Height);

            var center = new SDPoint(screenRect.X + screenRect.Width / 2,
                                     screenRect.Y + screenRect.Height / 2);

            return new EnhancedScreenCapture.MatchResult(
                screenRect,
                center,
                hit.Score,
                hit.Scale,
                hit.IsHardPass
            );
        }

        /// <summary>
        /// Ждет появления шаблона в окне (стрим-захват с таймаутом).
        /// </summary>
        public async Task<EnhancedScreenCapture.MatchResult?> WaitForAsync(
            IntPtr hwnd,
            string templateKey,
            TimeSpan timeout,
            int checkIntervalMs = 150,
            TemplateMatchOptions presets = null,
            bool ensureWindowReady = true,
            CancellationToken ct = default)
        {
            if (hwnd == IntPtr.Zero) throw new ArgumentException("hwnd is null");
            if (string.IsNullOrWhiteSpace(templateKey)) throw new ArgumentNullException(nameof(templateKey));

            if (ensureWindowReady)
                await EnsureWindowReadyAsync(hwnd, ct);

            var options = MapToMatchOptions(presets ?? TemplatePresets.Universal);
            var deadline = DateTime.UtcNow + (timeout <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : timeout);

            using var wgc = new EnhancedWindowsGraphicsCapture(_captureSettings);
            await wgc.InitializeAsync(hwnd);
            wgc.StartCapture();

            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    ct.ThrowIfCancellationRequested();

                    using var frame = await wgc.GetNextFrameAsync(ct);
                    using var mat = EnhancedScreenCapture.ConvertToMat(frame);

                    var hit = await _matcher.FindAsync(templateKey, mat, options);
                    if (hit is not null && hit.Score >= options.Threshold)
                    {
                        if (!GetWindowRect(hwnd, out var wr))
                            return null;

                        var screenRect = new SDRect(
                            wr.Left + hit.Bounds.X,
                            wr.Top + hit.Bounds.Y,
                            hit.Bounds.Width,
                            hit.Bounds.Height);

                        var center = new SDPoint(screenRect.X + screenRect.Width / 2,
                                                 screenRect.Y + screenRect.Height / 2);

                        return new EnhancedScreenCapture.MatchResult(
                            screenRect, center, hit.Score, hit.Scale, true);
                    }

                    await Task.Delay(checkIntervalMs, ct);
                }
                return null;
            }
            finally
            {
                wgc.StopCapture();
            }
        }

        // ---- helpers ----

        private static Abstractions.MatchOptions MapToMatchOptions(TemplateMatchOptions t)
        {
            var o = new Abstractions.MatchOptions
            {
                Threshold = t.Threshold,
                UseMultiScale = Math.Abs(t.ScaleMax - t.ScaleMin) > 1e-9,
                ScaleRange = new Abstractions.ScaleRange(t.ScaleMin, t.ScaleMax, t.ScaleStep),
                Preprocessing = new Abstractions.PreprocessingOptions
                {
                    UseGray = t.UseGray,
                    UseCanny = t.UseCanny,
                    Blur = t.Blur
                }
            };

            if (t.Roi is OpenCvSharp.Rect r && r.Width > 0 && r.Height > 0)
                o.SearchRegion = new SDRect(r.X, r.Y, r.Width, r.Height);

            return o;
        }

        /// <summary>
        /// Если окно свернуто или имеет «-32000» координаты — мягко восстанавливает без активации и
        /// отправляет вниз z-порядка. Ждет валидную геометрию.
        /// </summary>
        private static async Task EnsureWindowReadyAsync(IntPtr hwnd, CancellationToken ct)
        {
            var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
            GetWindowPlacement(hwnd, ref placement);

            if (placement.showCmd == SW_MINIMIZE)
            {
                ShowWindowAsync(hwnd, SW_RESTORE);
                SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOSENDCHANGING);
                await Task.Delay(150, ct);
            }

            // дождаться валидных координат
            for (int i = 0; i < 20; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (GetWindowRect(hwnd, out var r) && r.Width > 0 && r.Height > 0 && r.Left > -10000 && r.Top > -10000)
                    break;
                await Task.Delay(50, ct);
            }
        }

        public void Dispose()
        {
            _store?.Dispose();
        }

        // ---------- Win32 ----------
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_NOSENDCHANGING = 0x0400;

        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public RECT rcNormalPosition;
        }
    }
}
