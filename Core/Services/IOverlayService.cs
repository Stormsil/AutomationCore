// AutomationCore/Core/Services/IOverlayService.cs
using System;
using System.Windows;
using System.Windows.Media;

namespace AutomationCore.Core.Services
{
    public interface IOverlayService : IDisposable
    {
        void HighlightRegion(Rect rect, Color color, int ttlMs = 1000, int thickness = 3);
        void Show();
        void Hide();
        void Clear();
    }
}
