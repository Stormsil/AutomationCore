// AutomationCore/Core/Services/OverlayService.cs
using System;
using System.Drawing;
using AutomationCore.UI;

namespace AutomationCore.Core.Services
{
    public sealed class OverlayService : IOverlayService
    {
        private readonly SimpleOverlay _overlay = new();

        public void Show() => _overlay.Show();
        public void Hide() => _overlay.Hide();
        public void Clear() => _overlay.Clear();

        public void HighlightRegion(Rectangle rect, Color color, int ttlMs = 1000, int thickness = 3)
        {
            _overlay.Show();
            _overlay.SetClickThrough(true);
            _overlay.DrawBox(rect, color, thickness, ttlMs);
        }

        public void Dispose() => _overlay.Dispose();
    }
}
