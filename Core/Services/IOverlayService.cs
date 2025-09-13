// AutomationCore/Core/Services/IOverlayService.cs
using System;

namespace AutomationCore.Core.Services
{
    public interface IOverlayService : IDisposable
    {
        void HighlightRegion(System.Drawing.Rectangle rect,
                             System.Windows.Media.Color color,
                             int ttlMs = 1000,
                             int thickness = 3);
        void Show();
        void Hide();
        void Clear();
    }
}
