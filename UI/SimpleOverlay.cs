using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
// Алиасы для избежания конфликтов
using CvRect = OpenCvSharp.Rect;
using CvPoint = OpenCvSharp.Point;
using SDPoint = System.Drawing.Point;
using SDRect = System.Drawing.Rectangle;

namespace AutomationCore.UI
{
    /// <summary>
    /// Прозрачный overlay-рендерер:
    /// - Несколько типов примитивов (рамка, текст, линия, эллипс, картинка)
    /// - Слои и z-порядок
    /// - TTL для авто-очистки
    /// - Прикрепление к окну + авто-трекинг его bounds
    /// - По умолчанию клики проходят сквозь окно
    /// </summary>
    public sealed class SimpleOverlay : IDisposable
    {
        private Thread _uiThread;
        private OverlayForm _form;
        private readonly ManualResetEventSlim _ready = new(false);
        private bool _disposed;

        public SimpleOverlay()
        {
            _uiThread = new Thread(UIThread)
            {
                Name = "SimpleOverlay",
                IsBackground = true
            };
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();

            // ждём создания формы и handle
            _ready.Wait();
        }

        private void UIThread()
        {
            try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _form = new OverlayForm();
            _form.CreateControl();         // гарантируем Handle
            _ready.Set();
            Application.Run(_form);
        }

        /// <summary>Показать оверлей на всех мониторах (виртуальный экран).</summary>
        public void Show()
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() =>
            {
                _form.UnpinWindow();
                _form.FitToVirtualScreen();
                _form.Show();
                _form.BringToTopMost();
            }));
        }

        /// <summary>Скрыть оверлей.</summary>
        public void Hide()
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.Hide()));
        }

        /// <summary>
        /// Показать и прикрепить к конкретному окну (overlay ограничится рамками этого окна).
        /// При изменении положения/размера окна overlay будет следовать.
        /// </summary>
        public void ShowOverWindow(IntPtr hwnd, bool track = true)
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() =>
            {
                _form.PinToWindow(hwnd, track);
                _form.Show();
                _form.BringToTopMost();
            }));
        }

        /// <summary>Включить/выключить пропуск кликов сквозь overlay (по умолчанию включено).</summary>
        public void SetClickThrough(bool enabled)
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.SetClickThrough(enabled)));
        }

        /// <summary>Очистить все элементы.</summary>
        public void Clear()
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.ClearAll()));
        }

        /// <summary>Очистить элементы указанного слоя.</summary>
        public void ClearLayer(string layer)
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.ClearLayer(layer)));
        }

        // ---- Примитивы (System.Drawing) ----

        public void DrawBox(SDRect rect, Color color, int thickness = 3, int ttlMs = 0, string layer = "default", int cornerRadius = 0, DashStyle dash = DashStyle.Solid)
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.AddDrawable(new BoxDrawable(rect, color, thickness, ttlMs, layer, cornerRadius, dash))));
        }

        public void DrawText(SDPoint at, string text, Color color, float fontSize = 12f, int ttlMs = 0, string layer = "default", FontStyle style = FontStyle.Regular)
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.AddDrawable(new TextDrawable(at, text ?? string.Empty, color, fontSize, style, ttlMs, layer))));
        }

        public void DrawLine(SDPoint a, SDPoint b, Color color, int thickness = 2, int ttlMs = 0, string layer = "default", DashStyle dash = DashStyle.Solid)
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.AddDrawable(new LineDrawable(a, b, color, thickness, dash, ttlMs, layer))));
        }

        public void DrawEllipse(SDRect bounds, Color color, int thickness = 2, int ttlMs = 0, string layer = "default", DashStyle dash = DashStyle.Solid)
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.AddDrawable(new EllipseDrawable(bounds, color, thickness, dash, ttlMs, layer))));
        }

        /// <summary>Нарисовать картинку (Image не будет удерживаться — создаётся копия)</summary>
        public void DrawImage(Image image, SDRect dest, float opacity = 1f, int ttlMs = 0, string layer = "default")
        {
            if (image == null || image.Width <= 0 || image.Height <= 0) return;
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.AddDrawable(new ImageDrawable((Image)image.Clone(), dest, opacity, ttlMs, layer))));
        }

        // ---- Перегрузки для OpenCvSharp (удобно вызывать из кода с Rect/Point) ----

        public void DrawBox(CvRect rect, Color color, int thickness = 3, int ttlMs = 0, string layer = "default", int cornerRadius = 0, DashStyle dash = DashStyle.Solid)
            => DrawBox(ToSDRect(rect), color, thickness, ttlMs, layer, cornerRadius, dash);

        public void DrawText(CvPoint at, string text, Color color, float fontSize = 12f, int ttlMs = 0, string layer = "default", FontStyle style = FontStyle.Regular)
            => DrawText(ToSDPoint(at), text, color, fontSize, ttlMs, layer, style);

        public void DrawLine(CvPoint a, CvPoint b, Color color, int thickness = 2, int ttlMs = 0, string layer = "default", DashStyle dash = DashStyle.Solid)
            => DrawLine(ToSDPoint(a), ToSDPoint(b), color, thickness, ttlMs, layer, dash);

        public void DrawEllipse(CvRect bounds, Color color, int thickness = 2, int ttlMs = 0, string layer = "default", DashStyle dash = DashStyle.Solid)
            => DrawEllipse(ToSDRect(bounds), color, thickness, ttlMs, layer, dash);

        public void DrawImage(Image image, CvRect dest, float opacity = 1f, int ttlMs = 0, string layer = "default")
            => DrawImage(image, ToSDRect(dest), opacity, ttlMs, layer);

        private static SDRect ToSDRect(CvRect r) => new SDRect(r.X, r.Y, r.Width, r.Height);
        private static SDPoint ToSDPoint(CvPoint p) => new SDPoint(p.X, p.Y);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_form != null) _form.BeginInvoke(new Action(() => _form.Close()));
                _uiThread?.Join(1000);
            }
            catch { /* ignore */ }
            finally
            {
                _ready.Dispose();
                _form = null;
                _uiThread = null;
            }
        }

        // ================== Внутренняя форма ==================
        private sealed class OverlayForm : Form
        {
            // Примитивы: слой -> список
            private readonly ConcurrentDictionary<string, List<IDrawable>> _layers = new(StringComparer.OrdinalIgnoreCase);
            private readonly object _drawLock = new();

            // Рисование / очистка / трекинг
            private readonly System.Windows.Forms.Timer _gcTimer;   // TTL очистка и периодический Invalidate
            private readonly System.Windows.Forms.Timer _pinTimer;  // трекинг окна, если прикреплены

            // Pin-to-window
            private IntPtr _pinnedHwnd = IntPtr.Zero;
            private SDRect _lastPinnedRect;
            private bool _trackPinnedWindow = true;

            // Поведение
            private bool _clickThrough = true;

            public OverlayForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;

                BackColor = Color.Magenta;        // «прозрачный» цвет
                TransparencyKey = BackColor;

                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);

                // Таймер рендеринга/очистки (≈30 FPS)
                _gcTimer = new System.Windows.Forms.Timer { Interval = 33 };
                _gcTimer.Tick += (s, e) => { ScavengeExpired(); Invalidate(); };
                _gcTimer.Start();

                // Трекинг окна
                _pinTimer = new System.Windows.Forms.Timer { Interval = 100 };
                _pinTimer.Tick += (s, e) => TrackPinnedWindow();
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    const int WS_EX_TOOLWINDOW = 0x00000080;
                    const int WS_EX_NOACTIVATE = 0x08000000;
                    const int WS_EX_TRANSPARENT = 0x00000020;

                    var cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                    if (_clickThrough) cp.ExStyle |= WS_EX_TRANSPARENT;
                    return cp;
                }
            }

            public void SetClickThrough(bool enabled)
            {
                if (_clickThrough == enabled) return;
                _clickThrough = enabled;
                // Обновим extended styles
                var cp = CreateParams; // re-evaluate
                SetWindowLong(Handle, GWL_EXSTYLE, cp.ExStyle);
            }

            public void FitToVirtualScreen()
            {
                var vs = SystemInformation.VirtualScreen;
                Bounds = new SDRect(vs.Left, vs.Top, vs.Width, vs.Height);
            }

            public void PinToWindow(IntPtr hwnd, bool track)
            {
                _pinnedHwnd = hwnd;
                _trackPinnedWindow = track;

                if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
                {
                    // fallback — на весь виртуальный экран
                    UnpinWindow();
                    FitToVirtualScreen();
                    return;
                }

                if (GetWindowRect(hwnd, out var r))
                {
                    Bounds = r;
                    _lastPinnedRect = r;
                }

                if (_trackPinnedWindow) _pinTimer.Start(); else _pinTimer.Stop();
            }

            public void UnpinWindow()
            {
                _pinnedHwnd = IntPtr.Zero;
                _pinTimer.Stop();
            }

            private void TrackPinnedWindow()
            {
                var hwnd = _pinnedHwnd;
                if (hwnd == IntPtr.Zero) { _pinTimer.Stop(); return; }
                if (!IsWindow(hwnd)) { _pinTimer.Stop(); return; }

                if (!GetWindowRect(hwnd, out var r)) return;

                if (r != _lastPinnedRect)
                {
                    _lastPinnedRect = r;
                    Bounds = r;
                }
            }

            public void BringToTopMost()
            {
                const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
                SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }

            // ===== Управление примитивами =====

            public void AddDrawable(IDrawable drawable)
            {
                if (drawable == null) return;

                lock (_drawLock)
                {
                    if (!_layers.TryGetValue(drawable.Layer, out var list))
                    {
                        list = new List<IDrawable>(capacity: 16);
                        _layers[drawable.Layer] = list;
                    }
                    list.Add(drawable);
                }
            }

            public void ClearAll()
            {
                lock (_drawLock)
                {
                    foreach (var kv in _layers)
                    {
                        foreach (var d in kv.Value) d.Dispose();
                        kv.Value.Clear();
                    }
                    _layers.Clear();
                }
                Invalidate();
            }

            public void ClearLayer(string layer)
            {
                if (string.IsNullOrEmpty(layer)) return;

                lock (_drawLock)
                {
                    if (_layers.TryGetValue(layer, out var list))
                    {
                        foreach (var d in list) d.Dispose();
                        list.Clear();
                        _layers.TryRemove(layer, out _);
                    }
                }
                Invalidate();
            }

            private void ScavengeExpired()
            {
                var now = DateTime.UtcNow;

                lock (_drawLock)
                {
                    foreach (var kv in _layers)
                    {
                        var list = kv.Value;
                        if (list.Count == 0) continue;

                        for (int i = list.Count - 1; i >= 0; i--)
                        {
                            if (list[i].IsExpired(now))
                            {
                                list[i].Dispose();
                                list.RemoveAt(i);
                            }
                        }
                    }
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                // полностью прозрачный фон
                e.Graphics.Clear(TransparencyKey);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Рисуем слои в порядке добавления ключей. При необходимости — можно ввести явный z-order.
                lock (_drawLock)
                {
                    foreach (var kv in _layers)
                    {
                        var list = kv.Value;
                        for (int i = 0; i < list.Count; i++)
                        {
                            try { list[i].Draw(g); }
                            catch { /* защитимся от любых исключений в пользовательских данных */ }
                        }
                    }
                }
            }

            protected override void Dispose(bool disposing)
            {
                try
                {
                    _pinTimer?.Stop();
                    _gcTimer?.Stop();
                }
                catch { }

                if (disposing)
                {
                    lock (_drawLock)
                    {
                        foreach (var kv in _layers)
                        {
                            foreach (var d in kv.Value) d.Dispose();
                            kv.Value.Clear();
                        }
                        _layers.Clear();
                    }
                }

                base.Dispose(disposing);
            }

            // ===== WinAPI =====
            [DllImport("user32.dll")]
            private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                int X, int Y, int cx, int cy, uint uFlags);

            [DllImport("user32.dll")]
            private static extern bool GetWindowRect(IntPtr hWnd, out SDRect lpRect);

            [DllImport("user32.dll")]
            private static extern bool IsWindow(IntPtr hWnd);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

            private const int GWL_EXSTYLE = -20;
        }

        // ================== Примитивы для рисования ==================

        private interface IDrawable : IDisposable
        {
            string Layer { get; }
            DateTime? ExpiresAtUtc { get; }
            bool IsExpired(DateTime nowUtc);
            void Draw(Graphics g);
        }

        private abstract class DrawableBase : IDrawable
        {
            public string Layer { get; }
            public DateTime? ExpiresAtUtc { get; }

            protected DrawableBase(int ttlMs, string layer)
            {
                Layer = string.IsNullOrWhiteSpace(layer) ? "default" : layer;
                if (ttlMs > 0) ExpiresAtUtc = DateTime.UtcNow.AddMilliseconds(ttlMs);
            }

            public bool IsExpired(DateTime nowUtc) => ExpiresAtUtc.HasValue && nowUtc >= ExpiresAtUtc.Value;

            public abstract void Draw(Graphics g);

            public virtual void Dispose() { }
        }

        private sealed class BoxDrawable : DrawableBase
        {
            private readonly SDRect _rect;
            private readonly Color _color;
            private readonly int _thickness;
            private readonly int _cornerRadius;
            private readonly DashStyle _dash;

            public BoxDrawable(SDRect rect, Color color, int thickness, int ttlMs, string layer, int cornerRadius, DashStyle dash)
                : base(ttlMs, layer)
            {
                _rect = rect;
                _color = color;
                _thickness = Math.Max(1, thickness);
                _cornerRadius = Math.Max(0, cornerRadius);
                _dash = dash;
            }

            public override void Draw(Graphics g)
            {
                using var pen = new Pen(_color, _thickness) { LineJoin = LineJoin.Round, DashStyle = _dash };
                if (_cornerRadius <= 0)
                {
                    g.DrawRectangle(pen, _rect);
                }
                else
                {
                    using var path = RoundedRect(_rect, _cornerRadius);
                    g.DrawPath(pen, path);
                }
            }

            private static GraphicsPath RoundedRect(SDRect bounds, int radius)
            {
                int d = radius * 2;
                var gp = new GraphicsPath();
                gp.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
                gp.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
                gp.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
                gp.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
                gp.CloseFigure();
                return gp;
            }
        }

        private sealed class LineDrawable : DrawableBase
        {
            private readonly SDPoint _a, _b;
            private readonly Color _color;
            private readonly int _thickness;
            private readonly DashStyle _dash;

            public LineDrawable(SDPoint a, SDPoint b, Color color, int thickness, DashStyle dash, int ttlMs, string layer)
                : base(ttlMs, layer)
            {
                _a = a; _b = b; _color = color; _thickness = Math.Max(1, thickness); _dash = dash;
            }

            public override void Draw(Graphics g)
            {
                using var pen = new Pen(_color, _thickness) { DashStyle = _dash, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, _a, _b);
            }
        }

        private sealed class EllipseDrawable : DrawableBase
        {
            private readonly SDRect _bounds;
            private readonly Color _color;
            private readonly int _thickness;
            private readonly DashStyle _dash;

            public EllipseDrawable(SDRect bounds, Color color, int thickness, DashStyle dash, int ttlMs, string layer)
                : base(ttlMs, layer)
            {
                _bounds = bounds; _color = color; _thickness = Math.Max(1, thickness); _dash = dash;
            }

            public override void Draw(Graphics g)
            {
                using var pen = new Pen(_color, _thickness) { DashStyle = _dash };
                g.DrawEllipse(pen, _bounds);
            }
        }

        private sealed class TextDrawable : DrawableBase
        {
            private readonly SDPoint _pos;
            private readonly string _text;
            private readonly Color _color;
            private readonly float _fontSize;
            private readonly FontStyle _style;

            public TextDrawable(SDPoint pos, string text, Color color, float fontSize, FontStyle style, int ttlMs, string layer)
                : base(ttlMs, layer)
            {
                _pos = pos; _text = text ?? string.Empty; _color = color; _fontSize = Math.Max(6f, fontSize); _style = style;
            }

            public override void Draw(Graphics g)
            {
                using var font = new Font("Segoe UI", _fontSize, _style, GraphicsUnit.Point);
                // лёгкая тень для читаемости
                using var shadow = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
                using var brush = new SolidBrush(_color);
                var p = _pos;
                g.DrawString(_text, font, shadow, p.X + 1, p.Y + 1);
                g.DrawString(_text, font, brush, p.X, p.Y);
            }
        }

        private sealed class ImageDrawable : DrawableBase
        {
            private readonly Image _image;
            private readonly SDRect _dest;
            private readonly float _opacity; // 0..1

            public ImageDrawable(Image image, SDRect dest, float opacity, int ttlMs, string layer)
                : base(ttlMs, layer)
            {
                _image = image;
                _dest = dest;
                _opacity = Math.Max(0f, Math.Min(1f, opacity));
            }

            public override void Draw(Graphics g)
            {
                if (_image == null) return;

                if (_opacity >= 0.999f)
                {
                    g.DrawImage(_image, _dest);
                    return;
                }

                // Применим ColorMatrix для прозрачности
                using var ia = new ImageAttributes();
                var cm = new ColorMatrix
                {
                    Matrix00 = 1f,
                    Matrix11 = 1f,
                    Matrix22 = 1f,
                    Matrix33 = _opacity,
                    Matrix44 = 1f
                };
                ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                g.DrawImage(_image, _dest, 0, 0, _image.Width, _image.Height, GraphicsUnit.Pixel, ia);
            }

            public override void Dispose()
            {
                try { _image?.Dispose(); } catch { }
            }
        }
    }
}
