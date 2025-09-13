// AutomationCore/UI/SimpleOverlay.cs
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;

namespace AutomationCore.UI
{
    /// <summary>
    /// Тонкая совместимая обёртка над WpfOverlay.
    /// Оставляет старый API (Rectangle/Point/Color), но рисует через WPF.
    /// </summary>
    public sealed class SimpleOverlay : IDisposable
    {
        private readonly WpfOverlay _wpf = new();

        public void Show() => _wpf.Show();
        public void Hide() => _wpf.Hide();
        public void Clear() => _wpf.Clear();
        public void SetClickThrough(bool enabled) => _wpf.SetClickThrough(enabled);

        public void DrawBox(Rectangle rect, System.Drawing.Color color, int thickness = 3, int ttlMs = 0)
        {
            _wpf.DrawBox(ToRect(rect), ToMediaColor(color), thickness, ttlMs);
        }

        public void DrawText(Point at, string text, System.Drawing.Color color, float fontSize = 12f, int ttlMs = 0)
        {
            _wpf.DrawText(new System.Windows.Point(at.X, at.Y), text, ToMediaColor(color), fontSize, ttlMs);
        }

        public void Dispose() => _wpf.Dispose();

        // ---- helpers ----
        private static System.Windows.Rect ToRect(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
        private static System.Windows.Media.Color ToMediaColor(System.Drawing.Color c)
            => System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
    }
}
