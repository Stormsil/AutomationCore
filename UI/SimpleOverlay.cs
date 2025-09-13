// AutomationCore/UI/SimpleOverlay.cs
using System;

namespace AutomationCore.UI
{
    /// <summary>
    /// Тонкая совместимая обёртка над WpfOverlay.
    /// Внешний API — System.Drawing.*, внутри всё конвертируется в WPF.
    /// </summary>
    public sealed class SimpleOverlay : IDisposable
    {
        private readonly WpfOverlay _wpf = new();

        public void Show() => _wpf.Show();
        public void Hide() => _wpf.Hide();
        public void Clear() => _wpf.Clear();
        public void SetClickThrough(bool enabled) => _wpf.SetClickThrough(enabled);

        public void DrawBox(System.Drawing.Rectangle rect, System.Drawing.Color color, int thickness = 3, int ttlMs = 0)
        {
            var wpfRect = new System.Windows.Rect(rect.X, rect.Y, rect.Width, rect.Height);
            var wpfColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
            _wpf.DrawBox(wpfRect, wpfColor, thickness, ttlMs);
        }

        public void DrawText(System.Drawing.Point at, string text, System.Drawing.Color color, float fontSize = 12f, int ttlMs = 0)
        {
            var wpfPoint = new System.Windows.Point(at.X, at.Y);
            var wpfColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
            _wpf.DrawText(wpfPoint, text, wpfColor, fontSize, ttlMs);
        }

        public void Dispose() => _wpf.Dispose();
    }
}
