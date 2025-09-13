// AutomationCore/Core/Services/IOverlayService.cs
using System;
using System.Drawing;

namespace AutomationCore.Core.Services
{
    public interface IOverlayService : IDisposable
    {
        void HighlightRegion(Rectangle rect, Color color, int ttlMs = 1000, int thickness = 3);
        void Show();
        void Hide();
        void Clear();
    }
}
