// AutomationCore/UI/WpfOverlay.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AutomationCore.Infrastructure.UI
{
    public sealed class WpfOverlay : IDisposable
    {
        private Thread _uiThread;
        private OverlayWindow _win;
        private readonly ManualResetEventSlim _ready = new(false);

        public WpfOverlay()
        {
            _uiThread = new Thread(UIThread) { IsBackground = true, Name = "WpfOverlay" };
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            _ready.Wait();
        }

        private void UIThread()
        {
            var app = new System.Windows.Application();
            _win = new OverlayWindow();
            _ready.Set();
            app.Run(_win);
        }

        public void Show() => _win?.Dispatcher.Invoke(() => { _win.FitToVirtualScreen(); _win.ShowTop(); });
        public void Hide() => _win?.Dispatcher.Invoke(() => _win.Hide());
        public void Clear() => _win?.Dispatcher.Invoke(() => _win.Clear());
        public void SetClickThrough(bool enabled) => _win?.Dispatcher.Invoke(() => _win.SetClickThrough(enabled));

        public void DrawBox(System.Windows.Rect rect, System.Windows.Media.Color color, int thickness = 3, int ttlMs = 0)
            => _win?.Dispatcher.Invoke(() => _win.DrawBox(rect, color, thickness, ttlMs));

        public void DrawText(System.Windows.Point at, string text, System.Windows.Media.Color color, double fontSize = 12, int ttlMs = 0)
            => _win?.Dispatcher.Invoke(() => _win.DrawText(at, text, color, fontSize, ttlMs));

        public void Dispose()
        {
            try { _win?.Dispatcher.Invoke(() => _win.Close()); _uiThread?.Join(1000); }
            catch { /* ignore */ }
            finally { _ready.Dispose(); _win = null; _uiThread = null; }
        }

        private sealed class OverlayWindow : Window
        {
            private readonly Canvas _canvas = new();
            private readonly DispatcherTimer _gcTimer;
            private readonly List<(UIElement el, DateTime? exp)> _items = new();

            public OverlayWindow()
            {
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = System.Windows.Media.Brushes.Transparent;
                Topmost = true;
                ShowInTaskbar = false;
                Focusable = false;

                Content = _canvas;
                _canvas.IsHitTestVisible = false;

                _gcTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                _gcTimer.Tick += (_, __) => Scavenge();
                _gcTimer.Start();

                Loaded += (_, __) => ApplyExtendedStyles();
            }

            public void ShowTop()
            {
                if (!IsVisible) Show();
                Topmost = true; Topmost = false; Topmost = true;
            }

            public void FitToVirtualScreen()
            {
                Left = SystemParameters.VirtualScreenLeft;
                Top = SystemParameters.VirtualScreenTop;
                Width = SystemParameters.VirtualScreenWidth;
                Height = SystemParameters.VirtualScreenHeight;
            }

            public void Clear()
            {
                _canvas.Children.Clear();
                _items.Clear();
            }

            public void DrawBox(System.Windows.Rect rect, System.Windows.Media.Color color, int thickness, int ttlMs)
            {
                var r = new System.Windows.Shapes.Rectangle
                {
                    Width = Math.Max(0, rect.Width),
                    Height = Math.Max(0, rect.Height),
                    Stroke = Make(color),
                    StrokeThickness = Math.Max(1, thickness),
                    Fill = System.Windows.Media.Brushes.Transparent
                };
                Canvas.SetLeft(r, rect.X);
                Canvas.SetTop(r, rect.Y);
                _canvas.Children.Add(r);
                _items.Add((r, ttlMs > 0 ? DateTime.UtcNow.AddMilliseconds(ttlMs) : null));
            }

            public void DrawText(System.Windows.Point at, string text, System.Windows.Media.Color color, double fontSize, int ttlMs)
            {
                var tb = new TextBlock
                {
                    Text = text ?? string.Empty,
                    Foreground = Make(color),
                    FontSize = Math.Max(6, fontSize),
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
                };
                Canvas.SetLeft(tb, at.X);
                Canvas.SetTop(tb, at.Y);
                _canvas.Children.Add(tb);
                _items.Add((tb, ttlMs > 0 ? DateTime.UtcNow.AddMilliseconds(ttlMs) : null));
            }

            private static SolidColorBrush Make(System.Windows.Media.Color c)
            {
                var b = new SolidColorBrush(c);
                b.Freeze();
                return b;
            }

            private void Scavenge()
            {
                var now = DateTime.UtcNow;
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    var (el, exp) = _items[i];
                    if (exp.HasValue && now >= exp.Value)
                    {
                        _canvas.Children.Remove(el);
                        _items.RemoveAt(i);
                    }
                }
                if (_items.Count > 0) InvalidateVisual();
            }

            public void SetClickThrough(bool enabled)
            {
                var helper = new WindowInteropHelper(this);
                var hwnd = helper.EnsureHandle();
                int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                if (enabled)
                    ex |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                else
                    ex &= ~(WS_EX_TRANSPARENT);
                SetWindowLong(hwnd, GWL_EXSTYLE, ex);
            }

            private void ApplyExtendedStyles() => SetClickThrough(true);

            // Win32
            private const int GWL_EXSTYLE = -20;
            private const int WS_EX_TRANSPARENT = 0x00000020;
            private const int WS_EX_TOOLWINDOW = 0x00000080;
            private const int WS_EX_NOACTIVATE = 0x08000000;

            [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
            [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        }
    }
}
