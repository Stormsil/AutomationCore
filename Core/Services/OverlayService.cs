// AutomationCore/Core/Services/OverlayService.cs
using AutomationCore.UI;

namespace AutomationCore.Core.Services
{
    public sealed class OverlayService : IOverlayService
    {
        private readonly WpfOverlay _overlay = new();

        public void Show() => _overlay.Show();
        public void Hide() => _overlay.Hide();
        public void Clear() => _overlay.Clear();

        public void HighlightRegion(System.Drawing.Rectangle rect,
                                    System.Windows.Media.Color color,
                                    int ttlMs = 1000,
                                    int thickness = 3)
        {
            _overlay.Show();
            _overlay.SetClickThrough(true);

            var wpfRect = new System.Windows.Rect(rect.X, rect.Y, rect.Width, rect.Height);
            _overlay.DrawBox(wpfRect, color, thickness, ttlMs);
        }

        public void Dispose() => _overlay.Dispose();
    }
}
