// AutomationCore/Core/Capture/WgcCaptureFactory.cs
using System;
using System.Drawing;
using System.Threading.Tasks;
using AutomationCore.Capture;
using AutomationCore.Core.Abstractions;

namespace AutomationCore.Core.Capture
{
    /// <summary>Фабрика, выдающая адаптер над EnhancedScreenCapture под IScreenCapture.</summary>
    public class WgcCaptureFactory : ICaptureFactory
    {
        public IScreenCapture CreateCapture() => new WgcScreenCapture();
    }

    /// <summary>
    /// Лёгкий адаптер IScreenCapture над EnhancedWindowsGraphicsCapture/EnhancedScreenCapture.
    /// Для одиночного кадра используем WGC напрямую; для стримов — EnhancedScreenCapture.
    /// </summary>
    internal sealed class WgcScreenCapture : IScreenCapture
    {
        public bool IsSupported => EnhancedWindowsGraphicsCapture.IsSupported();

        public async Task<CaptureResult> CaptureAsync(CaptureRequest request)
        {
            var settings = request.Settings ?? CaptureSettings.Default;

            using var wgc = new EnhancedWindowsGraphicsCapture(settings);
            if (request.Target is WindowTarget wt)
                await wgc.InitializeAsync(wt.Window);
            else if (request.Target is ScreenTarget st)
                await wgc.InitializeAsync(IntPtr.Zero, st.MonitorIndex);
            else
                throw new ArgumentException("Unknown capture target");

            using var frame = await wgc.CaptureFrameAsync();

            var data = new byte[frame.Stride * frame.Height];
            for (int y = 0; y < frame.Height; y++)
            {
                Buffer.BlockCopy(frame.Data, y * frame.Stride, data, y * frame.Stride, frame.Stride);
            }

            return new CaptureResult
            {
                Data = data,
                Width = frame.Width,
                Height = frame.Height,
                Stride = frame.Stride,
                Timestamp = frame.Timestamp
            };

        }

        public async Task<ICaptureSession> StartSessionAsync(CaptureRequest request)
        {
            var settings = request.Settings ?? CaptureSettings.Default;

            var esc = new EnhancedScreenCapture(settings);
            if (request.Target is WindowTarget wt)
            {
                var session = await esc.StartCaptureSessionAsync(wt.Window);
                return session; // CaptureSession уже реализует ICaptureSession
            }
            else if (request.Target is ScreenTarget st)
            {
                var session = await esc.StartScreenCaptureAsync(st.MonitorIndex);
                return session;
            }

            throw new ArgumentException("Unknown capture target");
        }

        public void Dispose() { /* no-op */ }
    }
}
